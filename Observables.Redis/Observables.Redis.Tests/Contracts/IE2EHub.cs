using Observables.Redis;
using R3;

namespace Observables.Redis.Tests.Contracts;

[Redis]
public interface IE2EHub
{
    [RedisSubscribe("e2e.ping")]
    Observable<string> Ping { get; }

    [RedisPublish("e2e.ping")]
    Observable<Unit> PublishPing(string payload);

    [RedisPublish("e2e.news.{topic}")]
    Observable<Unit> PublishNews(string topic, string payload, CancellationToken cancellationToken = default);

    [RedisSubscribe("e2e.bytes")]
    Observable<byte[]> Bytes { get; }

    [RedisPublish("e2e.bytes")]
    Observable<Unit> PublishBytes(byte[] payload);
}
