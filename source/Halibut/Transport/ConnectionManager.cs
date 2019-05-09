using System;
using System.Collections.Generic;
using System.Linq;
using Halibut.Diagnostics;

namespace Halibut.Transport
{
    public class ConnectionManager : IDisposable
    {
        readonly ConnectionPool<ServiceEndPoint, IConnection> pool = new ConnectionPool<ServiceEndPoint, IConnection>();
        readonly Dictionary<ServiceEndPoint, HashSet<IConnection>> activeConnections = new Dictionary<ServiceEndPoint, HashSet<IConnection>>();

        public IConnection AcquireConnection(IConnectionFactory connectionFactory, ServiceEndPoint serviceEndpoint, ILog log)
        {
            lock (activeConnections)
            {
                var connection = GetFromPoolOrCreateConnection(connectionFactory, serviceEndpoint, log);
                AddConnectionToActiveConnections(serviceEndpoint, connection);

                return connection;
            }
        }

        IConnection GetFromPoolOrCreateConnection(IConnectionFactory connectionFactory, ServiceEndPoint serviceEndpoint, ILog log)
        {
            var connection = pool.Take(serviceEndpoint);
            if (connection == null)
            {
                connection = connectionFactory.EstablishNewConnection(serviceEndpoint, log);
                connection.OnDisposed += OnConnectionDisposed;
            }

            return connection;
        }

        void AddConnectionToActiveConnections(ServiceEndPoint serviceEndpoint, IConnection connection)
        {
            if (activeConnections.TryGetValue(serviceEndpoint, out var connections))
            {
                connections.Add(connection);
            }
            else
            {
                connections = new HashSet<IConnection> {connection};
                activeConnections.Add(serviceEndpoint, connections);
            }
        }

        public void ReleaseConnection(ServiceEndPoint serviceEndpoint, IConnection connection)
        {
            lock (activeConnections)
            {
                pool.Return(serviceEndpoint, connection);
                if (activeConnections.TryGetValue(serviceEndpoint, out var connections))
                {
                    connections.Remove(connection);
                }
            }
        }

        public void ClearPooledConnections(ServiceEndPoint serviceEndPoint, ILog log)
        {
            pool.Clear(serviceEndPoint, log);
        }

        public IReadOnlyCollection<IConnection> GetActiveConnections(ServiceEndPoint serviceEndPoint)
        {
            lock (activeConnections)
            {
                if (activeConnections.TryGetValue(serviceEndPoint, out var value))
                {
                    return value.ToArray();
                }
            }

            return Enumerable.Empty<IConnection>().ToArray();
        }

        public void Disconnect(ServiceEndPoint serviceEndPoint, ILog log)
        {
            ClearPooledConnections(serviceEndPoint, log);
            ClearActiveConnections(serviceEndPoint);
        }

        public void Dispose()
        {
            pool.Dispose();
        }


        void ClearActiveConnections(ServiceEndPoint serviceEndPoint)
        {
            lock (activeConnections)
            {
                if (activeConnections.TryGetValue(serviceEndPoint, out var activeConnectionsForEndpoint))
                {
                    foreach (var connection in activeConnectionsForEndpoint)
                    {
                        connection.Dispose();
                    }
                }
            }
        }

        void OnConnectionDisposed(object sender, EventArgs e)
        {
            lock (activeConnections)
            {
                var connection = sender as IConnection;

                var setsContainingConnection = activeConnections.Where(c => c.Value.Contains(connection)).ToList();
                var setsToRemoveCompletely = setsContainingConnection.Where(c => c.Value.Count == 1).ToList();
                foreach (var setContainingConnection in setsContainingConnection.Except(setsToRemoveCompletely))
                {
                    setContainingConnection.Value.Remove(connection);
                }

                foreach (var setToRemoveCompletely in setsToRemoveCompletely)
                {
                    activeConnections.Remove(setToRemoveCompletely.Key);
                }
            }
        }
    }
}