// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TestPlatform.CrossPlatEngine.UnitTests.Client
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestPlatform.Common.ExtensionFramework;
    using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Client;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Host;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Moq;

    using TestPlatform.Common.UnitTests.ExtensionFramework;

    [TestClass]
    public class ProxyDiscoveryManagerTests
    {
        private readonly DiscoveryCriteria discoveryCriteria;

        private ProxyDiscoveryManager testDiscoveryManager;

        private Mock<ITestRuntimeProvider> mockTestHostManager;

        private Mock<ITestRequestSender> mockRequestSender;

        private Mock<IDataSerializer> mockDataSerializer;

        /// <summary>
        /// The client connection timeout in milliseconds for unit tests.
        /// </summary>
        private int testableClientConnectionTimeout = 400;

        public ProxyDiscoveryManagerTests()
        {
            this.mockTestHostManager = new Mock<ITestRuntimeProvider>();
            this.mockRequestSender = new Mock<ITestRequestSender>();
            this.mockDataSerializer = new Mock<IDataSerializer>();
            this.testDiscoveryManager = new ProxyDiscoveryManager(
                                            this.mockRequestSender.Object,
                                            this.mockTestHostManager.Object,
                                            this.mockDataSerializer.Object,
                                            this.testableClientConnectionTimeout);
            this.discoveryCriteria = new DiscoveryCriteria(new[] { "test.dll" }, 1, string.Empty);

            // Default setup test host manager as shared (desktop)
            this.mockTestHostManager.SetupGet(th => th.Shared).Returns(true);
            this.mockTestHostManager.Setup(tmh => tmh.LaunchTestHostAsync(It.IsAny<TestProcessStartInfo>(), It.IsAny<CancellationToken>()))
                .Callback(
                    () =>
                        {
                            this.mockTestHostManager.Raise(thm => thm.HostLaunched += null, new HostProviderEventArgs(string.Empty));
                        })
                .Returns(Task.FromResult(true));
        }

        [TestMethod]
        public void DiscoverTestsShouldNotInitializeExtensionsOnNoExtensions()
        {
            // Make sure TestPlugincache is refreshed.
            TestPluginCache.Instance = null;

            this.mockRequestSender.Setup(s => s.WaitForRequestHandlerConnection(It.IsAny<int>())).Returns(true);

            this.testDiscoveryManager.DiscoverTests(this.discoveryCriteria, null);

            this.mockRequestSender.Verify(s => s.InitializeDiscovery(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public void DiscoverTestsShouldNotInitializeExtensionsOnCommunicationFailure()
        {
            // Make sure TestPlugincache is refreshed.
            TestPluginCache.Instance = null;

            this.mockRequestSender.Setup(s => s.WaitForRequestHandlerConnection(It.IsAny<int>())).Returns(false);

            Mock<ITestDiscoveryEventsHandler> mockTestDiscoveryEventHandler = new Mock<ITestDiscoveryEventsHandler>();

            this.testDiscoveryManager.DiscoverTests(this.discoveryCriteria, mockTestDiscoveryEventHandler.Object);

            this.mockRequestSender.Verify(s => s.InitializeExecution(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()), Times.Never);
        }


        [TestMethod]
        public void DiscoverTestsShouldNotSendDiscoveryRequestIfCommunicationFails()
        {
            this.mockTestHostManager.Setup(tmh => tmh.LaunchTestHostAsync(It.IsAny<TestProcessStartInfo>(), It.IsAny<CancellationToken>()))
                .Callback(
                    () =>
                        {
                            this.mockTestHostManager.Raise(thm => thm.HostLaunched += null, new HostProviderEventArgs(string.Empty));
                        })
                .Returns(Task.FromResult(false));

            this.mockRequestSender.Setup(s => s.WaitForRequestHandlerConnection(It.IsAny<int>())).Returns(true);

            // Make sure TestPlugincache is refreshed.
            TestPluginCache.Instance = null;

            Mock<ITestDiscoveryEventsHandler> mockTestDiscoveryEventHandler = new Mock<ITestDiscoveryEventsHandler>();

            this.testDiscoveryManager.DiscoverTests(this.discoveryCriteria, mockTestDiscoveryEventHandler.Object);

            this.mockRequestSender.Verify(s => s.DiscoverTests(It.IsAny<DiscoveryCriteria>(), It.IsAny<ITestDiscoveryEventsHandler>()), Times.Never);
        }

        [TestMethod]
        public void DiscoverTestsShouldInitializeExtensionsIfPresent()
        {
            // Make sure TestPlugincache is refreshed.
            TestPluginCache.Instance = null;

            try
            {
                var extensions = new[] { "c:\\e1.dll", "c:\\e2.dll" };

                // Setup Mocks.
                TestPluginCacheTests.SetupMockAdditionalPathExtensions(extensions);
                this.mockRequestSender.Setup(s => s.WaitForRequestHandlerConnection(It.IsAny<int>())).Returns(true);
                this.mockTestHostManager.Setup(th => th.GetTestPlatformExtensions(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>())).Returns(new[] { "c:\\e1.dll", "c:\\e2.dll" });

                this.testDiscoveryManager.DiscoverTests(this.discoveryCriteria, null);

                // Also verify that we have waited for client connection.
                this.mockRequestSender.Verify(s => s.InitializeDiscovery(extensions, true), Times.Once);
            }
            finally
            {
                TestPluginCache.Instance = null;
            }
        }

        [TestMethod]
        public void DiscoverTestsShouldQueryTestHostManagerForExtensions()
        {
            TestPluginCache.Instance = null;
            try
            {
                TestPluginCacheTests.SetupMockAdditionalPathExtensions(new[] { "c:\\e1.dll" });
                this.mockRequestSender.Setup(s => s.WaitForRequestHandlerConnection(It.IsAny<int>())).Returns(true);
                this.mockTestHostManager.Setup(th => th.GetTestPlatformExtensions(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>())).Returns(new[] { "he1.dll", "c:\\e1.dll" });

                this.testDiscoveryManager.DiscoverTests(this.discoveryCriteria, null);

                this.mockRequestSender.Verify(s => s.InitializeDiscovery(new[] { "he1.dll", "c:\\e1.dll" }, true), Times.Once);
            }
            finally
            {
                TestPluginCache.Instance = null;
            }
        }

        [TestMethod]
        public void DiscoverTestsShouldPassAdapterToTestHostManagerFromTestPluginCacheExtensions()
        {
            // We are updating extension with testadapter only to make it easy to test.
            // In product code it filter out testadapter from extension
            TestPluginCache.Instance.UpdateExtensions(new List<string> { "abc.TestAdapter.dll", "xyz.TestAdapter.dll" }, false);
            try
            {
                this.mockRequestSender.Setup(s => s.WaitForRequestHandlerConnection(It.IsAny<int>())).Returns(true);

                var expectedResult = new List<string>();
                expectedResult.AddRange(TestPluginCache.Instance.PathToExtensions);
                expectedResult.AddRange(TestPluginCache.Instance.DefaultExtensionPaths);

                this.testDiscoveryManager.DiscoverTests(this.discoveryCriteria, null);

                this.mockTestHostManager.Verify(th => th.GetTestPlatformExtensions(It.IsAny<IEnumerable<string>>(), expectedResult), Times.Once);
            }
            finally
            {
                TestPluginCache.Instance = null;
            }
        }

        [TestMethod]
        public void DiscoverTestsShouldNotIntializeTestHost()
        {
            // Setup mocks.
            this.mockRequestSender.Setup(s => s.WaitForRequestHandlerConnection(It.IsAny<int>())).Returns(true);

            // Act.
            this.testDiscoveryManager.DiscoverTests(this.discoveryCriteria, null);

            this.mockRequestSender.Verify(s => s.InitializeCommunication(), Times.Once);
            this.mockTestHostManager.Verify(thl => thl.LaunchTestHostAsync(It.IsAny<TestProcessStartInfo>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void DiscoverTestsShouldCatchExceptionAndCallHandleDiscoveryComplete()
        {
            // Setup mocks.
            Mock<ITestDiscoveryEventsHandler> mockTestDiscoveryEventsHandler = new Mock<ITestDiscoveryEventsHandler>();
            this.mockTestHostManager.Setup(tmh => tmh.LaunchTestHostAsync(It.IsAny<TestProcessStartInfo>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(false));

            // Act.
            this.testDiscoveryManager.DiscoverTests(this.discoveryCriteria, mockTestDiscoveryEventsHandler.Object);

            // Verify
            mockTestDiscoveryEventsHandler.Verify(s => s.HandleDiscoveryComplete(-1, It.IsAny<IEnumerable<TestCase>>(), true));
            mockTestDiscoveryEventsHandler.Verify(s => s.HandleRawMessage(It.IsAny<string>()));
            mockTestDiscoveryEventsHandler.Verify(s => s.HandleLogMessage(TestMessageLevel.Error, It.IsAny<string>()));
        }

        [TestMethod]
        public void DiscoverTestsShouldInitiateServerDiscoveryLoop()
        {
            // Setup mocks.
            this.mockRequestSender.Setup(s => s.WaitForRequestHandlerConnection(It.IsAny<int>())).Returns(true);

            // Act.
            this.testDiscoveryManager.DiscoverTests(this.discoveryCriteria, null);

            // Assert.
            this.mockRequestSender.Verify(s => s.DiscoverTests(It.IsAny<DiscoveryCriteria>(), null), Times.Once);
        }
    }
}
