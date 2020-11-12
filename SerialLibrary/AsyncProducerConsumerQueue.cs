using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SerialLibrary
{
    public class AsyncProducerConsumerQueue<T> : IDisposable
    {
        private Action<T> m_consumer;
        private readonly BlockingCollection<T> m_queue;
        private readonly CancellationTokenSource m_cancelTokenSrc;

        public AsyncProducerConsumerQueue(Action<T> consumer)
        {
            if (consumer == null)
            {
                throw new ArgumentNullException(nameof(consumer));
            }

            m_consumer = consumer;
            m_queue = new BlockingCollection<T>(new ConcurrentQueue<T>());
            m_cancelTokenSrc = new CancellationTokenSource();

            new Thread(() => ConsumeLoop(m_cancelTokenSrc.Token)).Start();
            Console.WriteLine("Consume Thread Started");
        }

        public void Produce(T value)
        {
            m_queue.Add(value);
            //Console.WriteLine($"Queue Add: new Length is {m_queue.Count}");
        }

        public void SwitchConsumerActionTo(Action<T> newAction)
        {
            m_consumer = newAction;
        }


        private void ConsumeLoop(CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                try
                { 
                        var item = m_queue.Take(cancelToken);
                        //Console.WriteLine($"Queue Take: new Length is {m_queue.Count}");
                        m_consumer(item);
                    
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
            }
        }

        #region IDisposable

        private bool m_isDisposed;

        protected virtual void Dispose(bool disposing)
        {
            if (!m_isDisposed)
            {
                if (disposing)
                {
                    m_cancelTokenSrc.Cancel();
                    m_cancelTokenSrc.Dispose();
                    m_queue.Dispose();
                }

                m_isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}