using System.Reflection;
using FluentValidation;
using JobRadar.Application.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace JobRadar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // MediatR — scans this assembly for all IRequestHandler<,> implementations
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // Pipeline: Logging first, then Validation
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // FluentValidation — scans this assembly for all AbstractValidator<T> implementations
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
