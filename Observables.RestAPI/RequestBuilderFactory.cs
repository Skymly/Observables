#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.RestAPI
{
    interface IRequestBuilderFactory
    {
#if NET8_0_OR_GREATER
        IRequestBuilder<T> Create<
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.NonPublicMethods
            )] T>(RestApiSettings? settings);
        [RequiresUnreferencedCode("Refit uses reflection to analyze interface methods. Ensure referenced interfaces and DTOs are preserved when trimming.")]
        IRequestBuilder Create(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.NonPublicMethods
            )] Type refitInterfaceType,
            RestApiSettings? settings
        );
#else
        IRequestBuilder<T> Create<T>(RestApiSettings? settings);
        IRequestBuilder Create(Type refitInterfaceType, RestApiSettings? settings);
#endif
    }

    class RequestBuilderFactory : IRequestBuilderFactory
    {
#if NET8_0_OR_GREATER
        public IRequestBuilder<T> Create<
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.NonPublicMethods
            )] T>(RestApiSettings? settings = null)
#else
        public IRequestBuilder<T> Create<T>(RestApiSettings? settings = null)
#endif
        {
            return new CachedRequestBuilderImplementation<T>(
                new RequestBuilderImplementation<T>(settings)
            );
        }

#if NET8_0_OR_GREATER
        [RequiresUnreferencedCode("Refit uses reflection to analyze interface methods. Ensure referenced interfaces and DTOs are preserved when trimming.")]
        public IRequestBuilder Create(
            [DynamicallyAccessedMembers(
                DynamicallyAccessedMemberTypes.PublicMethods |
                DynamicallyAccessedMemberTypes.NonPublicMethods
            )] Type refitInterfaceType,
            RestApiSettings? settings = null
        )
#else
        public IRequestBuilder Create(Type refitInterfaceType, RestApiSettings? settings = null)
#endif
        {
            return new CachedRequestBuilderImplementation(
                new RequestBuilderImplementation(refitInterfaceType, settings)
            );
        }
    }
}
