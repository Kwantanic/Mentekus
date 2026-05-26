namespace Mentekus.Api.Features.Question.Requests;

public sealed record QuestionSimilarityRequest(string Text, int Limit = 5);

public sealed record QuestionSimilarityResponse(string Text, double Similarity);
