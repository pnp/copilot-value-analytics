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
        var stringStats = GetStats(fromData.StringSurveyAnswers);
        var intStats = GetStats(fromData.IntSurveyAnswers);
        var boolStats = GetStats(fromData.BooleanSurveyAnswers);

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

        var allPositiveResults = GetPositive<string>(Answers.StringSurveyAnswers)
                .Concat(GetPositive<int>(Answers.IntSurveyAnswers))
                .Concat(GetPositive(Answers.BooleanSurveyAnswers));


        var allResultsCount = Answers.StringSurveyAnswers.Count() + Answers.IntSurveyAnswers.Count() + Answers.BooleanSurveyAnswers.Count();

        if (allPositiveResults.Count() == 0)
        {
            PercentageOfAnswersWithPositiveResult = 0;
        }
        else
        {
            PercentageOfAnswersWithPositiveResult = (long)(allPositiveResults.Count() * 100 / allResultsCount);
        }
    }

    private List<BaseSurveyAnswer> GetPositive<T>(IEnumerable<SurveyAnswer<T>> surveyAnswers) where T : notnull
    {
        List<BaseSurveyAnswer> allAnswers = new();
        foreach (var item in surveyAnswers)
        {
            try
            {
                item.SetIsPositiveResult();      // This might blow up if data is invalid
                if (item.IsPositiveResult!.Value)
                {
                    allAnswers.Add(item);
                }
            }
            catch (SurveyEngineDataException)
            {
                // Ignore invalid answers
            }
        }
        return allAnswers;
    }

    public SurveyAnswersCollection Answers { get; set; } = new();

    public long PercentageOfAnswersWithPositiveResult { get; set; }

    public QuestionStats Stats { get; set; } = new QuestionStats();

    static QuestionStats GetStats<T>(IEnumerable<SurveyAnswer<T>> datoir) where T : notnull
    {
        var stats = new QuestionStats();
        var allQuestions = datoir.Select(a => a.GetAnswerString()).Distinct();
        foreach (var q in allQuestions)
        {
            var answerResultsForQuestion = datoir
                .Where(a => a.GetAnswerString() == q);

            var positiveCount = 0;
            var negativeCount = 0;
            foreach (var item in answerResultsForQuestion)
            {
                try
                {
                    item.SetIsPositiveResult(); // This might blow up if data is invalid
                    if (item.IsPositiveResult!.Value)      
                    {
                        positiveCount++;
                    }
                    else
                    {
                        negativeCount++;
                    }
                }
                catch (SurveyEngineDataException)
                {
                    // Ignore invalid answers
                }
            }

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
