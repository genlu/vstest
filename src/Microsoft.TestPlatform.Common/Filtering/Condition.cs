// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.Common.Filtering
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using CommonResources = Microsoft.VisualStudio.TestPlatform.Common.Resources.Resources;

    internal enum Operation
    {
        Equal,
        NotEqual,
        Contains,
    }

    /// <summary>
    /// Operator in order of precedence.
    /// Predence(And) > Predence(Or)
    /// Precdence of OpenBrace and CloseBrace operators is not used, instead parsing code takes care of same.
    /// </summary>
    internal enum Operator
    {
        None,
        Or,
        And,
        OpenBrace,
        CloseBrace,
    }

    /// <summary>
    /// Represents a condition in filter expression.
    /// </summary>
    internal class Condition
    {
        #region Fields
        /// <summary>
        /// String seperator used for parsing input string of format '<propertyName>Operation<propertyValue>'
        /// ! is not a valid operation, but is required to filter the invalid patterns.
        /// </summary>
        private static string propertyNameValueSeperatorString = @"(\!\=)|(\=)|(\~)|(\!)";

        /// <summary>
        ///  Default property name which will be used when filter has only property value.
        /// </summary>
        public const string DefaultPropertyName = "FullyQualifiedName";

        /// <summary>
        ///  Default operation which will be used when filter has only property value.
        /// </summary>
        public const Operation DefaultOperation = Operation.Contains;

        /// <summary>
        /// Name of the property used in condition.
        /// </summary>
        internal string Name
        {
            get;
            private set;
        }

        /// <summary>
        /// Value for the property.
        /// </summary>
        internal string Value
        {
            get;
            private set;
        }

        /// <summary>
        /// Operation to be performed.
        /// </summary>
        internal Operation Operation
        {
            get;
            private set;
        }
        #endregion

        #region Constructors
        internal Condition(string name, Operation operation, string value)
        {
            this.Name = name;
            this.Operation = operation;
            this.Value = value;
        }
        #endregion


        /// <summary>
        /// Evaluate this condition for testObject.
        /// </summary>
        internal bool Evaluate(Func<string, Object> propertyValueProvider)
        {
            ValidateArg.NotNull(propertyValueProvider, "propertyValueProvider");
            var result = false;
            var hasValue = TryGetPropertyValue(propertyValueProvider, out var singleValue, out var multiValue);
            switch (this.Operation)
            {
                case Operation.Equal:
                    if (hasValue)
                    {
                        if (singleValue != null)
                        {
                            result = string.Equals(singleValue, Value, StringComparison.OrdinalIgnoreCase);
                        }
                        else
                        {
                            // if any value in multi-valued property matches 'this.Value', for Equal to evaluate true.
                            foreach (string propertyValue in multiValue)
                            {
                                result = result || string.Equals(propertyValue, Value, StringComparison.OrdinalIgnoreCase);
                                if (result)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    break;


                case Operation.NotEqual:
                    // all values in multi-valued property should not match 'this.Value' for NotEqual to evaluate true.
                    result = true; 
                    
                    // if value is null.
                    if (hasValue)
                    {
                        if (singleValue != null)
                        {
                            result = !string.Equals(singleValue, Value, StringComparison.OrdinalIgnoreCase);
                        }
                        else
                        {
                            foreach (string propertyValue in multiValue)
                            {
                                result = result && !string.Equals(propertyValue, Value, StringComparison.OrdinalIgnoreCase);
                                if (!result)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    break;

                case Operation.Contains:
                    // if any value in mulit-valued property contains 'this.Value' for 'Contains' to be true.
                    if (hasValue)
                    {
                        if (singleValue != null)
                        {
                            result = singleValue.IndexOf(Value, StringComparison.OrdinalIgnoreCase) != -1;
                        }
                        else
                        {
                            foreach (string propertyValue in multiValue)
                            {
                                Debug.Assert(null != propertyValue, "PropertyValue can not be null.");
                                result = result || propertyValue.IndexOf(Value, StringComparison.OrdinalIgnoreCase) != -1;
                                if (result)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    break;
            }
            return result;
        }




        /// <summary>
        /// Returns a condition object after parsing input string of format '<propertyName>Operation<propertyValue>'
        /// </summary>
        internal static Condition Parse(string conditionString)
        {
            if (string.IsNullOrWhiteSpace(conditionString))
            {
                ThrownFormatExceptionForInvalidCondition(conditionString);
            }
            string[] parts = Regex.Split(conditionString, propertyNameValueSeperatorString);
            if (parts.Length == 1)
            {
                // If only parameter values is passed, create condition with default property name,
                // default operation and given condition string as parameter value.
                return new Condition(Condition.DefaultPropertyName, Condition.DefaultOperation, conditionString.Trim());
            }

            if (parts.Length != 3)
            {
                ThrownFormatExceptionForInvalidCondition(conditionString);
            }

            for (int index = 0; index < 3; index++)
            {
                if (string.IsNullOrWhiteSpace(parts[index]))
                {
                    ThrownFormatExceptionForInvalidCondition(conditionString);
                }
                parts[index] = parts[index].Trim();
            }

            Operation operation = GetOperator(parts[1]);
            Condition condition = new Condition(parts[0], operation, parts[2]);
            return condition;
        }

        private static void ThrownFormatExceptionForInvalidCondition(string conditionString)
        {
            throw new FormatException(string.Format(CultureInfo.CurrentCulture, CommonResources.TestCaseFilterFormatException,
                string.Format(CultureInfo.CurrentCulture, CommonResources.InvalidCondition, conditionString)));
        }


        /// <summary>
        /// Check if condition validates any property in properties.
        /// </summary>
        internal bool ValidForProperties(IEnumerable<String> properties, Func<string, TestProperty> propertyProvider)
        {
            bool valid = false;

            if (properties.Contains(this.Name, StringComparer.OrdinalIgnoreCase))
            {
                valid = true;

                // Check if operation ~ (Contains) is on property of type string.
                if (this.Operation == Operation.Contains)
                {
                    valid = this.ValidForContainsOperation(propertyProvider);
                }
            }
            return valid;
        }
        
        private bool ValidForContainsOperation(Func<string, TestProperty> propertyProvider)
        {
            bool valid = true;

            // It is OK for propertyProvider to be null, no syntax check will happen.

            // Check validity of operator only if related TestProperty is non-null.
            // if null, it might be custom validation ignore it.             
            if (null != propertyProvider)
            {
                TestProperty testProperty = propertyProvider(Name);
                if (null != testProperty)
                {
                    Type propertyType = testProperty.GetValueType();
                    valid = typeof(string) == propertyType ||
                            typeof(string[]) == propertyType;
                }
            }
            return valid;
        }

        /// <summary>
        /// Return Operation corresponding to the operationString
        /// </summary>
        private static Operation GetOperator(string operationString)
        {
            switch (operationString)
            {
                case "=":
                    return Operation.Equal;

                case "!=":
                    return Operation.NotEqual;

                case "~":
                    return Operation.Contains;
            }
            throw new FormatException(string.Format(CultureInfo.CurrentCulture, CommonResources.TestCaseFilterFormatException, string.Format(CultureInfo.CurrentCulture, CommonResources.InvalidOperator, operationString)));
        }

        /// <summary>
        /// Returns property value for Property using propertValueProvider.
        /// </summary>
        private bool TryGetPropertyValue(Func<string, Object> propertyValueProvider, out string singleValue, out string[] multiValue)
        {
            var propertyValue = propertyValueProvider(this.Name);
            if (null != propertyValue)
            {
                singleValue = propertyValue as string;
                multiValue = singleValue != null ? null : propertyValue as string[];
                return true;
            }

            singleValue = null;
            multiValue = null;
            return false;
        }

    }
}
