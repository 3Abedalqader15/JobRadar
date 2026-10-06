using System.Text.Json;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Enums;
using MediatR;

namespace JobRadar.Application.Features.JobApplications.Queries.GetJobApplicationQuestions;

public sealed class GetJobApplicationQuestionsQueryHandler 
    : IRequestHandler<GetJobApplicationQuestionsQuery, IReadOnlyList<JobQuestionDto>>
{
    private readonly IJobApplicationQuestionRepository _questionRepository;

    public GetJobApplicationQuestionsQueryHandler(IJobApplicationQuestionRepository questionRepository)
    {
        _questionRepository = questionRepository;
    }

    public async Task<IReadOnlyList<JobQuestionDto>> Handle(
        GetJobApplicationQuestionsQuery request, 
        CancellationToken cancellationToken)
    {
        var questions = await _questionRepository.GetByJobIdAsync(request.JobId, cancellationToken);

        return questions
            .OrderBy(q => q.DisplayOrder)
            .Select(q =>
            {
                List<string>? options = null;
                if (q.QuestionType == QuestionType.MultipleChoice && !string.IsNullOrWhiteSpace(q.OptionsJson))
                {
                    try
                    {
                        options = JsonSerializer.Deserialize<List<string>>(q.OptionsJson);
                    }
                    catch
                    {
                        options = new List<string>();
                    }
                }

                return new JobQuestionDto(
                    q.Id,
                    q.JobId,
                    q.QuestionText,
                    q.QuestionType,
                    options,
                    q.IsRequired,
                    q.DisplayOrder);
            })
            .ToList();
    }
}
