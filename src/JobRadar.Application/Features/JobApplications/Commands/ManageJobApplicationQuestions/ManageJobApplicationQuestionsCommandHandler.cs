using System.Text.Json;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Common.Exceptions;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using MediatR;

namespace JobRadar.Application.Features.JobApplications.Commands.ManageJobApplicationQuestions;

public sealed class ManageJobApplicationQuestionsCommandHandler : IRequestHandler<ManageJobApplicationQuestionsCommand>
{
    private readonly IJobRepository _jobRepository;
    private readonly IJobApplicationQuestionRepository _questionRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public ManageJobApplicationQuestionsCommandHandler(
        IJobRepository jobRepository,
        IJobApplicationQuestionRepository questionRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _jobRepository = jobRepository;
        _questionRepository = questionRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ManageJobApplicationQuestionsCommand request, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(request.JobId, cancellationToken);
        if (job is null)
        {
            throw new NotFoundException(nameof(Job), request.JobId);
        }

        // Rejection for aggregated jobs (Job.CompanyId is null)
        if (!job.CompanyId.HasValue)
        {
            throw new InvalidOperationException("Cannot add screening questions to an aggregated job.");
        }

        // Enforce HR scoping: HR users can only manage screening questions for jobs belonging to their company
        if (_currentUserService.IsHr && _currentUserService.CompanyId != job.CompanyId)
        {
            throw new ForbiddenException("HR users can only manage screening questions for jobs belonging to their assigned company.");
        }

        var existingQuestions = await _questionRepository.GetByJobIdAsync(request.JobId, cancellationToken);
        var existingDict = existingQuestions.ToDictionary(q => q.Id);

        // 1. Sync or add submitted questions
        var submittedIds = new HashSet<Guid>();

        foreach (var qDto in request.Questions)
        {
            string? optionsJson = qDto.QuestionType == QuestionType.MultipleChoice && qDto.Options != null
                ? JsonSerializer.Serialize(qDto.Options)
                : null;

            if (qDto.Id.HasValue && existingDict.TryGetValue(qDto.Id.Value, out var existingQuestion))
            {
                submittedIds.Add(existingQuestion.Id);
                existingQuestion.Update(
                    qDto.QuestionText,
                    qDto.QuestionType,
                    optionsJson,
                    qDto.IsRequired,
                    qDto.DisplayOrder);

                await _questionRepository.UpdateAsync(existingQuestion, cancellationToken);
            }
            else
            {
                var newQuestion = JobApplicationQuestion.Create(
                    request.JobId,
                    qDto.QuestionText,
                    qDto.QuestionType,
                    optionsJson,
                    qDto.IsRequired,
                    qDto.DisplayOrder);

                await _questionRepository.AddAsync(newQuestion, cancellationToken);
            }
        }

        // 2. Remove questions that were deleted from the list
        var toDelete = existingQuestions.Where(q => !submittedIds.Contains(q.Id)).ToList();
        foreach (var questionToDelete in toDelete)
        {
            await _questionRepository.DeleteAsync(questionToDelete, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
