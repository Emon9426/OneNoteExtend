using System;
using System.Collections.Generic;

namespace OneExtend.Addin
{
    /// <summary>Minimal service locator handed to commands via ICommandContext.</summary>
    public sealed class ServiceRegistry : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<T>(T instance)
        {
            _services[typeof(T)] = instance;
        }

        public object GetService(Type serviceType) =>
            _services.TryGetValue(serviceType, out var svc) ? svc : null;

        public T Resolve<T>() where T : class =>
            GetService(typeof(T)) as T;
    }
}
