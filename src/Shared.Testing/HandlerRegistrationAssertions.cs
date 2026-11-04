using MediatR;
using Shouldly;
using System.Reflection;

namespace Shared.Testing;

public static class HandlerRegistrationAssertions
{
	/// <summary>
	/// Fails when the Application assembly holds a MediatR handler that the test service provider
	/// cannot resolve. This keeps a context's test fixture in step with new handlers.
	/// </summary>
	public static void AssertEveryHandlerIsRegistered(Assembly applicationAssembly, IServiceProvider provider, string fixtureName)
	{
		var handlerTypes = applicationAssembly.GetTypes().Where(IsRequestHandler).ToList();

		handlerTypes.ShouldNotBeEmpty();

		var unregistered = handlerTypes
			.Where(type => provider.GetService(type) is null)
			.Select(type => type.Name)
			.ToList();

		unregistered.ShouldBeEmpty(
			$"Register these handlers in {fixtureName}.RegisterHandlers: " + string.Join(", ", unregistered));
	}

	private static bool IsRequestHandler(Type type)
	{
		return type is { IsClass: true, IsAbstract: false }
			&& type.GetInterfaces().Any(i => i.IsGenericType
				&& (i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)
					|| i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)));
	}
}