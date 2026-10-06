using FluentValidation;
using FluentValidation.Results;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Exceptions;
using JobRadar.Domain.Entities;
using MediatR;

namespace JobRadar.Application.Features.Companies.Commands.UpdateCompany;

public sealed class UpdateCompanyCommandHandler : IRequestHandler<UpdateCompanyCommand>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCompanyCommandHandler(
        ICompanyRepository companyRepository,
        IUnitOfWork unitOfWork)
    {
        _companyRepository = companyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Company), request.Id);

        var normSlug = request.Slug.Trim().ToLowerInvariant();

        if (await _companyRepository.ExistsBySlugAsync(normSlug, request.Id, cancellationToken))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.Slug), $"A company with slug '{normSlug}' already exists.")
            });
        }

        company.Update(request.Name, normSlug, request.LogoUrl);

        await _companyRepository.UpdateAsync(company, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
