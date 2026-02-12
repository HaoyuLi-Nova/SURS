using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SURS.App.Helpers
{
    /// <summary>
    /// 事件订阅管理器：统一管理事件订阅，防止内存泄漏
    /// </summary>
    public class EventSubscriptionManager : IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        /// <summary>
        /// 订阅 PropertyChanged 事件
        /// </summary>
        public void SubscribePropertyChanged(INotifyPropertyChanged source, PropertyChangedEventHandler handler)
        {
            if (source == null || handler == null) return;
            
            source.PropertyChanged += handler;
            _subscriptions.Add(new PropertyChangedSubscription(source, handler));
        }

        /// <summary>
        /// 订阅 CollectionChanged 事件
        /// </summary>
        public void SubscribeCollectionChanged(INotifyCollectionChanged source, NotifyCollectionChangedEventHandler handler)
        {
            if (source == null || handler == null) return;
            
            source.CollectionChanged += handler;
            _subscriptions.Add(new CollectionChangedSubscription(source, handler));
        }

        /// <summary>
        /// 清理所有订阅
        /// </summary>
        public void UnsubscribeAll()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
            _subscriptions.Clear();
        }

        public void Dispose()
        {
            UnsubscribeAll();
        }

        #region 内部订阅类

        private class PropertyChangedSubscription : IDisposable
        {
            private readonly INotifyPropertyChanged _source;
            private readonly PropertyChangedEventHandler _handler;

            public PropertyChangedSubscription(INotifyPropertyChanged source, PropertyChangedEventHandler handler)
            {
                _source = source;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_source != null && _handler != null)
                {
                    _source.PropertyChanged -= _handler;
                }
            }
        }

        private class CollectionChangedSubscription : IDisposable
        {
            private readonly INotifyCollectionChanged _source;
            private readonly NotifyCollectionChangedEventHandler _handler;

            public CollectionChangedSubscription(INotifyCollectionChanged source, NotifyCollectionChangedEventHandler handler)
            {
                _source = source;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_source != null && _handler != null)
                {
                    _source.CollectionChanged -= _handler;
                }
            }
        }

        #endregion
    }
}

