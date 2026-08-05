using Observables.Redis;

namespace Observables.Redis.Reactive.Tests.Contracts;

[Redis]
public interface IE2EHubReactive
{
    [RedisSubscribe("e2e.ping")]
    IObservable<string> Ping { get; }

    [RedisPublish("e2e.ping")]
    IObservable<System.Reactive.Unit> PublishPing(string payload);

    [RedisPublish("e2e.news.{topic}")]
    IObservable<System.Reactive.Unit> PublishNews(string topic, string payload, CancellationToken cancellationToken = default);

    [RedisSubscribe("e2e.bytes")]
    IObservable<byte[]> Bytes { get; }

    [RedisPublish("e2e.bytes")]
    IObservable<System.Reactive.Unit> PublishBytes(byte[] payload);

    [RedisSubscribe("e2e.pattern.*")]
    IObservable<RedisMessage<string>> PatternEnvelope { get; }

    [RedisPublish("e2e.pattern.{topic}")]
    IObservable<System.Reactive.Unit> PublishPattern(string topic, string payload);
}
