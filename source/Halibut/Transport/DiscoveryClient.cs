using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Halibut.Diagnostics;

namespace Halibut.Transport
{
    public class DiscoveryClient
    {
        static readonly byte[] HelloLine = Encoding.ASCII.GetBytes("HELLO" + Environment.NewLine + Environment.NewLine);
        readonly LogFactory logs = new LogFactory();

        public ServiceEndPoint Discover(ServiceEndPoint serviceEndpoint)
        {
            return Discover(serviceEndpoint, CancellationToken.None);
        }

        public ServiceEndPoint Discover(ServiceEndPoint serviceEndpoint, CancellationToken cancellationToken)
        {
            try
            {
                var log = logs.ForEndpoint(serviceEndpoint.BaseUri);
                using (var client = TcpConnectionFactory.CreateConnectedTcpClient(serviceEndpoint, log, cancellationToken))
                {
                    using (var stream = client.GetStream())
                    {
                        using (var ssl = new SslStream(stream, false, ValidateCertificate))
                        {
                            ssl.AuthenticateAsClient(serviceEndpoint.BaseUri.Host, new X509Certificate2Collection(), SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false);
                            ssl.Write(HelloLine, 0, HelloLine.Length);
                            ssl.Flush();

                            if (ssl.RemoteCertificate == null)
                                throw new Exception("The server did not provide an SSL certificate");

#pragma warning disable PC001 // API not supported on all platforms - X509Certificate2 not supported on macOS
                            return new ServiceEndPoint(serviceEndpoint.BaseUri, new X509Certificate2(ssl.RemoteCertificate.Export(X509ContentType.Cert)).Thumbprint);
#pragma warning restore PC001 // API not supported on all platforms - X509Certificate2 not supported on macOS
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new HalibutClientException(ex.Message, ex);
            }
        }

        bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslpolicyerrors)
        {
            return true;
        }
    }
}