using System;
using System.Linq;
using System.Reflection;
using GraphQL.DataLoader;
using GraphQL.Execution;
using GraphQL.Types;
using GraphQL.Types.Relay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using GraphQL.DI;

namespace GraphQL.Server
{
    /// <summary>
    /// GraphQL specific extension methods for <see cref="IGraphQLBuilder"/>.
    /// </summary>
    public static class GraphQLBuilderCoreExtensions
    {
        /// <summary>
        /// Adds the GraphQL Relay types <see cref="ConnectionType{TNodeType}"/>, <see cref="EdgeType{TNodeType}"/>
        /// and <see cref="PageInfoType"/>.
        /// </summary>
        /// <param name="builder">GraphQL builder used for GraphQL specific extension methods as 'this' argument.</param>
        /// <returns>Reference to <paramref name="builder"/>.</returns>
        public static IGraphQLBuilder AddRelayGraphTypes(this IGraphQLBuilder builder)
        {
            builder
                .Services
                .AddSingleton(typeof(ConnectionType<>))
                .AddSingleton(typeof(EdgeType<>))
                .AddSingleton<PageInfoType>();

            return builder;
        }
    }
}
