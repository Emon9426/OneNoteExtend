using System;
using System.Collections.Generic;
using System.Linq;

namespace OneExtend.Core.Events
{
    public interface IEventBus
    {
        /// <summary>Deliver an event to all current subscribers of its type.</summary>
        void Publish<TEvent>(TEvent evt);

        /// <summary>Subscribe; dispose the token to unsubscribe.</summary>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler);

        int SubscriberCount<TEvent>();
    }

    /// <summary>
    /// Minimal synchronous pub/sub bus. OneNote-side events (page switched,
    /// selection changed, ...) are broadcast here so modules never call each
    /// other directly.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private readonly object _gate = new object();
        private readonly Dictionary<Type, List<Delegate>> _handlers =
            new Dictionary<Type, List<Delegate>>();

        public void Publish<TEvent>(TEvent evt)
        {
            List<Delegate> snapshot;
            lock (_gate)
            {
                if (!_handlers.TryGetValue(typeof(TEvent), out var list))
                    return;
                snapshot = list.ToList();
            }
            foreach (var d in snapshot)
            {
                try
                {
                    ((Action<TEvent>)d)(evt);
                }
                catch
                {
                    // A misbehaving subscriber must not break the publisher or its peers.
                }
            }
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            lock (_gate)
            {
                if (!_handlers.TryGetValue(typeof(TEvent), out var list))
                {
                    list = new List<Delegate>();
                    _handlers[typeof(TEvent)] = list;
                }
                list.Add(handler);
            }
            return new Subscription(() =>
            {
                lock (_gate)
                {
                    if (_handlers.TryGetValue(typeof(TEvent), out var list))
                        list.Remove(handler);
                }
            });
        }

        public int SubscriberCount<TEvent>()
        {
            lock (_gate)
            {
                return _handlers.TryGetValue(typeof(TEvent), out var list) ? list.Count : 0;
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;
            public Subscription(Action dispose) { _dispose = dispose; }
            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }
    }
}
