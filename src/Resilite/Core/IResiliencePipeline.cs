using System;

namespace Resilite;

public interface IResiliencePipeline
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action, 
        CancellationToken cancellationToken = default);

    Task ExecuteAsync(
        Func<CancellationToken, Task> action, 
        CancellationToken cancellationToken = default);
}
