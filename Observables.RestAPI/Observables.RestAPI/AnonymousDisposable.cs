namespace Observables.RestAPI
{
    sealed class AnonymousDisposable(Action block) : IDisposable
    {
        public void Dispose()
        {
            block();
        }
    }
}
