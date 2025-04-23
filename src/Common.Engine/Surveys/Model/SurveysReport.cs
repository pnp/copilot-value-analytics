using System.Text.Json.Serialization;

namespace Common.Engine.Surveys.Model;

public class SurveysReport
{
    public SurveysReport()
    {
    }
    public SurveysReport(SurveyAnswersCollection fromData) : this()
    {
        this.Answers = fromData;
        var stringStats = GetStats(fromData.StringSurveyAnswers.Cast<BaseSurveyAnswer>());
        var intStats = GetStats(fromData.IntSurveyAnswers.Cast<BaseSurveyAnswer>());
        var boolStats = GetStats(fromData.BooleanSurveyAnswers.Cast<BaseSurveyAnswer>());

        var allStats = new List<QuestionStats> { stringStats, intStats, boolStats };
        var compiledStats = new QuestionStats();
        foreach (var stat in allStats)
        {
            if (stat.IsEmpty)
            {
                continue;
            }
            else
            {
                if (stat.HighestPositiveAnswerQuestion.Score > compiledStats.HighestPositiveAnswerQuestion.Score)
                {
                    compiledStats.HighestPositiveAnswerQuestion = stat.HighestPositiveAnswerQuestion;
                }
                if (stat.HighestNegativeAnswerQuestion.Score > compiledStats.HighestNegativeAnswerQuestion.Score)
                {
                    compiledStats.HighestNegativeAnswerQuestion = stat.HighestNegativeAnswerQuestion;
                }
            }
        }
        Stats = compiledStats;

        var allResults = Answers.StringSurveyAnswers
                .Select(a => a.IsPositiveResult)
                .Concat(Answers.IntSurveyAnswers.Select(a => a.IsPositiveResult))
                .Concat(Answers.BooleanSurveyAnswers.Select(a => a.IsPositiveResult));

        if (allResults.Count() == 0)
        {
            PercentageOfAnswersWithPositiveResult = 0;
        }
        else
        {
            PercentageOfAnswersWithPositiveResult = (long)(allResults.Count(a => a) * 100 / allResults.Count());
        }
    }

    public SurveyAnswersCollection Answers { get; set; } = new();

    public long PercentageOfAnswersWithPositiveResult { get; set; }

    public QuestionStats Stats { get; set; } = new QuestionStats();

    static QuestionStats GetStats(IEnumerable<BaseSurveyAnswer> datoir)
    {
        var stats = new QuestionStats();
        var allQuestions = datoir.Select(a => a.GetAnswerString()).Distinct();
        foreach (var q in allQuestions)
        {
            var answerResultsForQuestion = datoir
                .Where(a => a.GetAnswerString() == q)
                .Select(a => a.IsPositiveResult);

            var positiveCount = answerResultsForQuestion.Count(positive => positive);
            var negativeCount = answerResultsForQuestion.Count(positive => !positive);

            if (positiveCount > stats.HighestPositiveAnswerQuestion.Score)
            {
                stats.HighestPositiveAnswerQuestion.Score = positiveCount;
                stats.HighestPositiveAnswerQuestion.Entity = q;
            }
            if (negativeCount > stats.HighestNegativeAnswerQuestion.Score)
            {
                stats.HighestNegativeAnswerQuestion.Score = negativeCount;
                stats.HighestNegativeAnswerQuestion.Entity = q;
            }
        }
        return stats;
    }
}

public class QuestionStats
{
    public EntityWithScore<string> HighestPositiveAnswerQuestion { get; set; } = new EntityWithScore<string>(string.Empty, int.MinValue);
    public EntityWithScore<string> HighestNegativeAnswerQuestion { get; set; } = new EntityWithScore<string>(string.Empty, int.MinValue);


    [JsonIgnore]
    public bool IsEmpty
    {
        get
        {
            return HighestPositiveAnswerQuestion.Score == int.MinValue || HighestNegativeAnswerQuestion.Score == int.MinValue;
        }
    }
}
