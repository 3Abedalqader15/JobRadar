using FluentValidation;
using FluentValidation.Results;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using MediatR;

namespace JobRadar.Application.Features.Companies.Commands.CreateCompany;

public sealed class CreateCompanyCommandHandler : IRequestHandler<CreateCompanyCommand, Guid>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCompanyCommandHandler(
        ICompanyRepository companyRepository,
        IUnitOfWork unitOfWork)
    {
        _companyRepository = companyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        var normSlug = request.Slug.Trim().ToLowerInvariant();

        if (await _companyRepository.ExistsBySlugAsync(normSlug, null, cancellationToken))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.Slug), $"A company with slug '{normSlug}' already exists.")
            });
        }

        var company = Company.Create(request.Name, normSlug, request.LogoUrl);

        await _companyRepository.AddAsync(company, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return company.Id;
    }
}
