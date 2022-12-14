using System;
using System.Net.Sockets;
using FluentAssertions;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests
{
    [TestFixture]
    public class TcpConnectionFactoryFixture
    {
        [Test]
        public void ShouldCreateDualModeIpv6Socket_WhenIPv6Enabled()
        {
            var client = TcpConnectionFactory.CreateTcpClient(AddressFamily.InterNetworkV6);
            client.Client.AddressFamily.Should().Be(AddressFamily.InterNetworkV6);
            client.Client.DualMode.Should().BeTrue();
        }

        [Test]
        public void ShouldCreateIpv4Socket_WhenIPv6Disabled()
        {
            var client = TcpConnectionFactory.CreateTcpClient(AddressFamily.InterNetwork);
            client.Client.AddressFamily.Should().Be(AddressFamily.InterNetwork);
            client.Invoking(c =>
            {
                var dualMode = c.Client.DualMode;
            }).Should().Throw<NotSupportedException>();
        }
    }
}