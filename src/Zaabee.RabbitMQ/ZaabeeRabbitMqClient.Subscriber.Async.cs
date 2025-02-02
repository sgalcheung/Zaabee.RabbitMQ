using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Zaabee.RabbitMQ
{
    public partial class ZaabeeRabbitMqClient
    {
        private readonly ConcurrentDictionary<string, IModel> _subscriberAsyncChannelDic = new();

        private IModel GetReceiverAsyncChannel(ExchangeParam exchangeParam, QueueParam queueParam,
            ushort prefetchCount)
        {
            return _subscriberAsyncChannelDic.GetOrAdd(queueParam.Queue, _ =>
            {
                var channel = _subscribeAsyncConn.CreateModel();

                channel.QueueDeclare(queue: queueParam.Queue, durable: queueParam.Durable,
                    exclusive: queueParam.Exclusive, autoDelete: queueParam.AutoDelete,
                    arguments: queueParam.Arguments);

                if (exchangeParam is not null)
                {
                    channel.ExchangeDeclare(exchange: exchangeParam.Exchange,
                        type: exchangeParam.Type.ToString().ToLower(),
                        durable: exchangeParam.Durable, autoDelete: exchangeParam.AutoDelete,
                        arguments: exchangeParam.Arguments);
                    channel.QueueBind(queue: queueParam.Queue, exchange: exchangeParam.Exchange,
                        routingKey: queueParam.Queue);
                }

                channel.BasicQos(0, prefetchCount, false);
                return channel;
            });
        }
        private Task ConsumeEventAsync<T>(IModel channel, Func<Action<T>> resolve, string queue)
        {
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var msg = _serializer.DeserializeFromBytes<T>(ea.Body.ToArray());
                    resolve.Invoke()(msg);
                }
                catch (Exception ex)
                {
                    PublishDlx<T>(ea, queue, ex);
                }
                finally
                {
                    channel.BasicAck(ea.DeliveryTag, false);
                    await Task.Yield();
                }
            };
            channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);
            return Task.CompletedTask;
        }

        private Task ConsumeEventAsync<T>(IModel channel, Func<Func<T, Task>> resolve, string queue)
        {
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var msg = _serializer.DeserializeFromBytes<T>(ea.Body.ToArray());
                    await resolve.Invoke()(msg);
                }
                catch (Exception ex)
                {
                    PublishDlx<T>(ea, queue, ex);
                }
                finally
                {
                    channel.BasicAck(ea.DeliveryTag, false);
                    await Task.Yield();
                }
            };
            channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);
            return Task.CompletedTask;
        }

        private Task ConsumeMessageAsync<T>(IModel channel, Func<Action<T>> resolve, string queue)
        {
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body;
                    var msg = _serializer.DeserializeFromBytes<T>(body.ToArray());
                    resolve.Invoke()(msg);
                }
                finally
                {
                    channel.BasicAck(ea.DeliveryTag, false);
                    await Task.Yield();
                }
            };
            channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);
            return Task.CompletedTask;
        }

        private Task ConsumeMessageAsync<T>(IModel channel, Func<Func<T, Task>> resolve, string queue)
        {
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body;
                    var msg = _serializer.DeserializeFromBytes<T>(body.ToArray());
                    await resolve.Invoke()(msg);
                }
                finally
                {
                    channel.BasicAck(ea.DeliveryTag, false);
                    await Task.Yield();
                }
            };
            channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);
            return Task.CompletedTask;
        }
    }
}