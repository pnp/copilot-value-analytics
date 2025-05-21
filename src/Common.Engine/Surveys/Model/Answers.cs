using Entities.DB.Entities;

namespace Common.Engine.Surveys.Model;

/// <summary>
/// Non-generic base class for survey answers.
/// </summary>
public abstract class BaseSurveyAnswer
{
    public bool? IsPositiveResult { get; set; }
    public abstract string GetAnswerString();
}

public abstract class SurveyAnswer<T> : BaseSurveyAnswer where T : notnull
{
    public SurveyAnswer()
    {
    }
    protected SurveyAnswer(SurveyQuestionResponseDB a) : this()
    {
        this.Id = a.ID;
    }

    public int Id { get; set; } = 0;
    public T ValueGiven { get; set; } = default!;

    public SurveyQuestion<T> Question { get; set; } = default!;

    public override string GetAnswerString()
    {
        return Question.QuestionText;
    }

    public bool IsValid => Question.OptimalAnswerLogicalOp != LogicalOperator.Unknown;

    public void SetIsPositiveResult()
    {
        if (Question.OptimalAnswerLogicalOp == LogicalOperator.Unknown)
            throw new SurveyEngineDataException($"{nameof(Question.OptimalAnswerLogicalOp)} is {nameof(LogicalOperator.Unknown)}");

        if (Question.OptimalAnswerLogicalOp == LogicalOperator.Equals)
        {
            this.IsPositiveResult = ValueGiven.Equals(Question.OptimalAnswer);
            return;
        }
        else if (Question.OptimalAnswerLogicalOp == LogicalOperator.NotEquals)
        {
            this.IsPositiveResult = !ValueGiven.Equals(Question.OptimalAnswer);
            return;
        }
        else if (Question.OptimalAnswerLogicalOp == LogicalOperator.GreaterThan)
        {
            this.IsPositiveResult = IsAnswerTrueForExpectedComparativeVal(LogicalOperator.GreaterThan);
            return;
        }
        else if (Question.OptimalAnswerLogicalOp == LogicalOperator.LessThan)
        {
            this.IsPositiveResult = IsAnswerTrueForExpectedComparativeVal(LogicalOperator.LessThan);
            return;
        }

        throw new SurveyEngineLogicException();

    }

    protected abstract bool IsAnswerTrueForExpectedComparativeVal(LogicalOperator op);
}

public class StringSurveyAnswer : SurveyAnswer<string>
{
    public StringSurveyAnswer() : base()
    {
    }
    public StringSurveyAnswer(SurveyQuestionResponseDB a) : base(a)
    {
        Question = new StringSurveyQuestion(a.ForQuestion, 0) { OptimalAnswer = a.ForQuestion.OptimalAnswerValue };
        ValueGiven = a.GivenAnswer;
    }

    protected override bool IsAnswerTrueForExpectedComparativeVal(LogicalOperator op)
    {
        throw new InvalidOperationException("String survey questions can't be greater/less than compared");
    }
}

public class IntSurveyAnswer : SurveyAnswer<int>
{
    public IntSurveyAnswer() : base()
    {
    }
    public IntSurveyAnswer(SurveyQuestionResponseDB a) : base(a)
    {
        Question = new IntSurveyQuestion(a.ForQuestion, 0);

        if (int.TryParse(a.GivenAnswer, out var r))
        {
            ValueGiven = r;
        }
        else
        {
            throw new SurveyEngineDataException("Failed to parse int answer");
        }
    }

    protected override bool IsAnswerTrueForExpectedComparativeVal(LogicalOperator op)
    {
        switch (op)
        {
            case LogicalOperator.Equals:
                return ValueGiven == Question.OptimalAnswer;
            case LogicalOperator.NotEquals:
                return ValueGiven != Question.OptimalAnswer;
            case LogicalOperator.GreaterThan:
                return ValueGiven > Question.OptimalAnswer;
            case LogicalOperator.LessThan:
                return ValueGiven < Question.OptimalAnswer;
            default:
                throw new SurveyEngineLogicException();
        }
    }
}

public class BooleanSurveyAnswer : SurveyAnswer<bool>
{
    public BooleanSurveyAnswer() : base()
    {
    }

    public BooleanSurveyAnswer(SurveyQuestionResponseDB a) : base(a)
    {
        Question = new BooleanSurveyQuestion(a.ForQuestion, 0);

        if (bool.TryParse(a.GivenAnswer, out var r))
        {
            ValueGiven = r;
        }
        else
        {
            throw new SurveyEngineDataException("Failed to parse boolean answer");
        }
    }

    protected override bool IsAnswerTrueForExpectedComparativeVal(LogicalOperator op)
    {
        throw new InvalidOperationException("Boolean survey questions can't be greater/less than compared");
    }
}

/// <summary>
/// A list of survey answers by user(s)
/// </summary>
public class SurveyAnswersCollection
{
    public SurveyAnswersCollection() { }        // Serialization constructor

    public SurveyAnswersCollection(List<SurveyQuestionResponseDB> answers)
    {
        foreach (var a in answers)
        {
            if (a.ForQuestion.DataType == QuestionDatatype.String)
            {
                StringSurveyAnswers.Add(new StringSurveyAnswer(a));
            }
            else if (a.ForQuestion.DataType == QuestionDatatype.Int)
            {
                IntSurveyAnswers.Add(new IntSurveyAnswer(a));
            }
            else if (a.ForQuestion.DataType == QuestionDatatype.Bool)
            {
                BooleanSurveyAnswers.Add(new BooleanSurveyAnswer(a));
            }
            else
            {
                throw new SurveyEngineDataException("Unknown question datatype");
            }
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public List<int> AllAnswerIds =>
        StringSurveyAnswers.Select(a => a.Id)
        .Concat(IntSurveyAnswers.Select(a => a.Id))
        .Concat(BooleanSurveyAnswers.Select(a => a.Id))
        .Distinct()
        .ToList();


    public List<IntSurveyAnswer> IntSurveyAnswers { get; set; } = new();
    public List<StringSurveyAnswer> StringSurveyAnswers { get; set; } = new();
    public List<BooleanSurveyAnswer> BooleanSurveyAnswers { get; set; } = new();
}
