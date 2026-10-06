using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Exceptions;
using JobRadar.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace JobRadar.Application.Features.Companies.Commands.AssignHrToCompany;

public sealed class AssignHrToCompanyCommandHandler : IRequestHandler<AssignHrToCompanyCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICompanyRepository _companyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AssignHrToCompanyCommandHandler(
        UserManager<ApplicationUser> userManager,
        ICompanyRepository companyRepository,
        IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _companyRepository = companyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(AssignHrToCompanyCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString())
            ?? throw new NotFoundException(nameof(ApplicationUser), request.UserId);

        var company = await _companyRepository.GetByIdAsync(request.CompanyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Company), request.CompanyId);

        // Ensure user has HR role
        if (!await _userManager.IsInRoleAsync(user, "HR"))
        {
            var roleResult = await _userManager.AddToRoleAsync(user, "HR");
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to assign HR role to user: {errors}");
            }
        }

        user.AssignCompany(company.Id);

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to update user's company: {errors}");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
