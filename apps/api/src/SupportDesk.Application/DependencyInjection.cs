using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Features.Agents.Queries.GetAgents;
using SupportDesk.Application.Features.Categories.Queries.GetCategories;
using SupportDesk.Application.Features.Customers.Queries.GetCustomer;
using SupportDesk.Application.Features.Customers.Queries.GetCustomers;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Queries.SearchTickets;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Application.Sla;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application;

/// <summary>
/// Registers the command and query handlers, the request validators and the SLA settings
/// validator. Binding the <c>Sla</c> section is left to the host, which owns configuration.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(sp => new SlaWindowPolicy(sp.GetRequiredService<IOptions<SlaOptions>>().Value.ToTerms()));
        services.AddScoped<TicketTriage>();

        services.AddScoped<RaiseTicketCommandHandler>();
        services.AddScoped<ChangeTicketStatusCommandHandler>();
        services.AddScoped<AssignTicketCommandHandler>();
        services.AddScoped<SearchTicketsQueryHandler>();
        services.AddScoped<GetTicketQueryHandler>();

        services.AddScoped<GetCustomersQueryHandler>();
        services.AddScoped<GetCustomerQueryHandler>();

        services.AddScoped<GetAgentsQueryHandler>();

        services.AddScoped<GetCategoriesQueryHandler>();

        services.AddValidatorsFromAssemblyContaining<RaiseTicketCommandHandler>(ServiceLifetime.Singleton);

        services.AddSingleton<IValidateOptions<SlaOptions>, SlaOptionsValidator>();

        return services;
    }
}
