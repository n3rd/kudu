using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.SiteManagement
{
    public sealed class X509StoreSearcher : IDisposable
    {
        private bool _disposed;
        private X509Store _store;

        public X509StoreSearcher(string storeName, StoreLocation location)
        {
            _store = new X509Store(storeName, location);
            Store.Open(OpenFlags.ReadOnly);
        }

        public X509Store Store
        {
            get { return _store; }
        }

        public X509Certificate2 FindByFriendlyName(string friendlyName)
        {
            if (_disposed) throw new ObjectDisposedException("X509StoreSearcher");

            return Store.Certificates
                        .OfType<X509Certificate2>()
                        .FirstOrDefault(cert => cert.FriendlyName.Equals(friendlyName, StringComparison.OrdinalIgnoreCase));
        }

        public X509Certificate2 FindByThumbprint(string thumbprint)
        {
            if (_disposed) throw new ObjectDisposedException("X509StoreSearcher");

            return Store.Certificates
                        .Find(X509FindType.FindByThumbprint, thumbprint, false)
                        .OfType<X509Certificate2>()
                        .FirstOrDefault();
        }

        public IEnumerable<X509Certificate2> FindAll()
        {
            if (_disposed) throw new ObjectDisposedException("X509StoreSearcher");

            return Store.Certificates
                        .OfType<X509Certificate2>();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Store.Close();
            _store = null;
            _disposed = true;
        }
    }
}
