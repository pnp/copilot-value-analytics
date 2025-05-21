using Common.Engine.Surveys;
using Common.Engine.Surveys.Model;
using Entities.DB.Entities;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using UnitTests.FakeLoaderClasses;

namespace UnitTests;

[TestClass]
public class SurveyTests : AbstractTest
{
    [TestMethod]
    public void SurveysReportTests()
    {
        var emptyStats = new QuestionStats();
        Assert.IsTrue(emptyStats.IsEmpty);

        var surveyReport = new SurveysReport(new SurveyAnswersCollection
        {
            StringSurveyAnswers = new List<StringSurveyAnswer>
            {
                new StringSurveyAnswer
                {
                    Question = new StringSurveyQuestion
                    {
                        QuestionText = "What is love?",
                        OptimalAnswer = "Don't hurt me",
                        OptimalAnswerLogicalOp = LogicalOperator.Equals,
                    },
                    ValueGiven = "Don't hurt me"
                }
            },
            IntSurveyAnswers = new List<IntSurveyAnswer>
            {
                new IntSurveyAnswer
                {
                    Question = new IntSurveyQuestion
                    {
                        QuestionText = "What is 1+1?",
                        OptimalAnswer = 2,
                        OptimalAnswerLogicalOp = LogicalOperator.Equals,
                    },
                    ValueGiven = 2
                }
            },
            BooleanSurveyAnswers = new List<BooleanSurveyAnswer>
            {
                new BooleanSurveyAnswer
                {
                    Question = new BooleanSurveyQuestion
                    {
                        QuestionText = "Is this true?",
                        OptimalAnswer = true,
                        OptimalAnswerLogicalOp = LogicalOperator.Equals,
                    },
                    ValueGiven = true
                },
                new BooleanSurveyAnswer
                {
                    Question = new BooleanSurveyQuestion
                    {
                        QuestionText = "Is this also true?",
                        OptimalAnswer = true,
                        OptimalAnswerLogicalOp = LogicalOperator.Equals,
                    },
                    ValueGiven = false          // Wrong answer
                }
            }
        }
        );

        Assert.AreEqual(75, surveyReport.PercentageOfAnswersWithPositiveResult);
        Assert.AreEqual("What is love?", surveyReport.Stats.HighestPositiveAnswerQuestion.Entity);
        Assert.AreEqual("Is this also true?", surveyReport.Stats.HighestNegativeAnswerQuestion.Entity);

        Assert.IsTrue(surveyReport.Stats.HighestPositiveAnswerQuestion.Score == 1);
        Assert.IsTrue(surveyReport.Stats.HighestNegativeAnswerQuestion.Score == 1);
    }

    [TestMethod]
    public void SurveyModelTests()
    {
        var pageDb = new SurveyPageDB
        {
            AdaptiveCardTemplateJson = @"
                {
                  ""type"": ""AdaptiveCard"",
                  ""version"": ""1.3"",
                  ""body"": [
                    {
                      ""type"": ""TextBlock"",
                      ""size"": ""Medium"",
                      ""text"": ""Answer this thus:"",
                      ""weight"": ""Bolder""
                    }
                  ],
                  ""actions"": [
                    {
                      ""type"": ""Action.Submit"",
                      ""title"": ""Update Survey""
                    }
                  ],
                  ""$schema"": ""http://adaptivecards.io/schemas/adaptive-card.json""
                }",
            Questions = new List<SurveyQuestionDefinitionDB>()
            {
                new() {
                    ID = 1,
                    DataType = QuestionDatatype.String,
                    OptimalAnswerValue = "Don't hurt me",
                    OptimalAnswerLogicalOp = LogicalOperator.Equals,
                    QuestionText = "What is love?",
                    Index = 0,
                },
                new() {
                    ID = 2,
                    DataType = QuestionDatatype.Int,
                    OptimalAnswerValue = "2",
                    OptimalAnswerLogicalOp = LogicalOperator.Equals,
                    QuestionText = "What is 1+1?",
                    Index = 2,
                },
                new() {
                    ID = 3,
                    DataType = QuestionDatatype.Bool,
                    OptimalAnswerValue = "True",
                    OptimalAnswerLogicalOp = LogicalOperator.Equals,
                    QuestionText = "True?",
                    Index = 4,
                },
            }
        };

        var survey = new SurveyPage(pageDb);

        // Check index values
        for (int i = 0; i < survey.AllQuestions.Count; i++)
        {
            Assert.AreEqual(survey.AllQuestions[i].Index, i);
        }

        Assert.AreEqual(3, survey.AllQuestions.Count);
        Assert.AreEqual(1, survey.StringQuestions.Count);
        Assert.AreEqual(1, survey.IntQuestions.Count);
        Assert.AreEqual(1, survey.BoolQuestions.Count);

        var json = survey.BuildAdaptiveCard();
        Assert.AreEqual("AdaptiveCard", json["type"]);
        Assert.AreEqual("1.3", json["version"]);
    }

    [TestMethod]
    public void SurveyPageUserResponseModelTests()
    {
        var responseJson = @"{""autoQuestionId-1"": ""a"", ""autoQuestionId-2"": ""1"" }";
        var test = new SurveyPageUserResponse(responseJson, "whoever");
        Assert.AreEqual(2, test.Answers.Count);
        Assert.IsTrue(test.IsValid);

        Assert.IsTrue(test.Answers.Contains(new SurveyPageUserResponse.RawResponse { QuestionId = 1, Response = "a" }));
        Assert.IsTrue(test.Answers.Contains(new SurveyPageUserResponse.RawResponse { QuestionId = 2, Response = "1" }));

        Assert.IsTrue(test.QuestionIds.Count == 2);
        Assert.IsTrue(test.QuestionIds.Contains(1));
        Assert.IsTrue(test.QuestionIds.Contains(2));


        var invalid1 = new SurveyPageUserResponse("rando", "whoever");
        Assert.IsFalse(invalid1.IsValid);

        var invalid2 = new SurveyPageUserResponse("{\"random-1\": \"a\"}", "whoever");
        Assert.IsTrue(invalid2.IsValid);
        Assert.AreEqual(0, invalid2.Answers.Count);
    }

    [TestMethod]
    public void AnswersCollectionModelTests()
    {
        var answers = new List<SurveyQuestionResponseDB>
        {
            new SurveyQuestionResponseDB
            {
                ID = 1,
                ForQuestion = new SurveyQuestionDefinitionDB
                {
                    DataType = QuestionDatatype.String,
                    OptimalAnswerValue = "Don't hurt me",
                    OptimalAnswerLogicalOp = LogicalOperator.Equals,
                    QuestionText = "What is love?",
                    Index = 0,
                },
                GivenAnswer = "Don't hurt me"
            },
            new SurveyQuestionResponseDB
            {
                ID = 2,
                ForQuestion = new SurveyQuestionDefinitionDB
                {
                    DataType = QuestionDatatype.Int,
                    OptimalAnswerValue = "2",
                    OptimalAnswerLogicalOp = LogicalOperator.Equals,
                    QuestionText = "What is 1+1?",
                    Index = 2,
                },
                GivenAnswer = "2"
            },
            new SurveyQuestionResponseDB
            {
                ID = 3,
                ForQuestion = new SurveyQuestionDefinitionDB
                {
                    DataType = QuestionDatatype.Bool,
                    OptimalAnswerValue = "True",
                    OptimalAnswerLogicalOp = LogicalOperator.Equals,
                    QuestionText = "True?",
                    Index = 4,
                },
                GivenAnswer = "True"
            }
        };

        var ac = new SurveyAnswersCollection(answers);
        Assert.AreEqual(3, ac.AllAnswerIds.Count);
        Assert.AreEqual(1, ac.StringSurveyAnswers.Count);
        Assert.AreEqual(1, ac.IntSurveyAnswers.Count);
        Assert.AreEqual(1, ac.BooleanSurveyAnswers.Count);
    }

    [TestMethod]
    public async Task SurveyManagerGetAllPublishedSurveyQuestionResponses()
    {
        var sm = new SurveyManager(
            new SqlSurveyManagerDataLoader(_db, GetLogger<SqlSurveyManagerDataLoader>()),
            new FakeSurveyProcessor(),
            GetLogger<SurveyManager>());


        var data = await sm.GetSurveyQuestionResponses();
        var r = new SurveysReport(data);

        Assert.IsNotNull(r);
    }


    [TestMethod]
    public async Task SurveySaveAndLoadTests()
    {

        var sm = new SurveyManager(
            new SqlSurveyManagerDataLoader(_db, GetLogger<SqlSurveyManagerDataLoader>()),
            new FakeSurveyProcessor(),
            GetLogger<SurveyManager>());

        var testPage = new SurveyPageDB { Name = "unit test", IsPublished = true };

        // Insert test data
        var firstUserInDb = new User { UserPrincipalName = "unittesting" + DateTime.Now.Ticks };
        _db.Users.Add(firstUserInDb);

        // Clear question survey responses
        var existingSurveys = await _db.SurveyQuestionResponses.ToListAsync();
        if (existingSurveys.Count > 0)
        {
            _db.SurveyQuestionResponses.RemoveRange(existingSurveys);
        }
        await _db.SaveChangesAsync();

        // Check that there are no existing survey responses
        var allResponses = await sm.GetSurveyQuestionResponses();
        Assert.IsTrue(allResponses.AllAnswerIds.Count == 0);

        var newStringQ = new SurveyQuestionDefinitionDB
        {
            DataType = QuestionDatatype.String,
            OptimalAnswerValue = "Don't hurt me",
            OptimalAnswerLogicalOp = LogicalOperator.Equals,
            QuestionText = "What is love? " + DateTime.Now.Ticks,
            Index = 0,
        };
        var newIntQ = new SurveyQuestionDefinitionDB
        {
            DataType = QuestionDatatype.Int,
            OptimalAnswerValue = "2",
            OptimalAnswerLogicalOp = LogicalOperator.Equals,
            QuestionText = "What is 1+1? " + DateTime.Now.Ticks,
            Index = 2,
        };
        var newBoolQ = new SurveyQuestionDefinitionDB
        {
            DataType = QuestionDatatype.Bool,
            OptimalAnswerValue = "True",
            OptimalAnswerLogicalOp = LogicalOperator.Equals,
            QuestionText = "True? " + DateTime.Now.Ticks,
            Index = 4,
        };
        _db.SurveyQuestionDefinitions.AddRange(newStringQ, newIntQ, newBoolQ);


        var newPage = new SurveyPageDB
        {
            Name = "unit test",
            IsPublished = true,
            AdaptiveCardTemplateJson = "{}",
            Questions = new List<SurveyQuestionDefinitionDB>
            {
                newStringQ,
                newIntQ,
                newBoolQ
            }
        };
        _db.SurveyPages.Add(newPage);

        var newSurvey = new SurveyGeneralResponseDB
        {
            OverrallRating = 5,
            Requested = DateTime.UtcNow,
            User = firstUserInDb,
            RelatedEvent = null,
        };
        _db.SurveyGeneralResponses.Add(newSurvey);

        await _db.SaveChangesAsync();


        var jsonSurveyPageUserResponse = new JObject
        {
            ["autoQuestionId-" + newStringQ.ID] = newStringQ.OptimalAnswerValue,
            ["autoQuestionId-" + newIntQ.ID] = newIntQ.OptimalAnswerValue,
            ["autoQuestionId-" + newBoolQ.ID] = newBoolQ.OptimalAnswerValue,
        };

        // Save the survey response
        var r = await sm.SaveCustomSurveyResponse(new SurveyPageUserResponse(jsonSurveyPageUserResponse.ToString(),
            firstUserInDb.UserPrincipalName), newSurvey.ID);

        allResponses = await sm.GetSurveyQuestionResponses();
        Assert.IsTrue(allResponses.AllAnswerIds.Count == 3);        // jsonSurveyPageUserResponse has 3

        Assert.IsTrue(r.AllAnswerIds.Count == 3);
        Assert.IsTrue(r.StringSurveyAnswers.Count == 1);
        Assert.IsTrue(r.IntSurveyAnswers.Count == 1);
        Assert.IsTrue(r.BooleanSurveyAnswers.Count == 1);

        // Find same answers in DB
        var dbAnwsers = await _db.SurveyQuestionResponses
            .Where(a => r.AllAnswerIds.Contains(a.ID))
            .ToListAsync();

        Assert.IsTrue(dbAnwsers.Count == 3);
    }

    /// <summary>
    /// Checks that the IsPositiveResult property of the survey answer is working correctly.
    /// </summary>
    [TestMethod]
    public void AnswerIsPositiveResultTests()
    {

        // Check unknown logical operator. Insufficient data to determine if the answer is positive
        Assert.ThrowsException<SurveyEngineDataException>(() =>
        {
            var unknownSurveyQ = new StringSurveyAnswer
            {
                Question = new StringSurveyQuestion
                {
                    QuestionText = "What is love?",
                    OptimalAnswer = "Don't hurt me",
                    OptimalAnswerLogicalOp = LogicalOperator.Unknown,
                },
                ValueGiven = "Don't hurt me",
            };
            unknownSurveyQ.SetIsPositiveResult();
        });

        var stringEqualsSurveyQ = new StringSurveyAnswer
        {
            Question = new StringSurveyQuestion
            {
                QuestionText = "What is love?",
                OptimalAnswer = "Don't hurt me",
                OptimalAnswerLogicalOp = LogicalOperator.Equals,
            },
            ValueGiven = "Don't hurt me"
        };

        // Check that the expected value is equal to the value given
        stringEqualsSurveyQ.SetIsPositiveResult();
        Assert.IsTrue(stringEqualsSurveyQ.IsPositiveResult);

        // Check a false value
        stringEqualsSurveyQ.Question.OptimalAnswerLogicalOp = LogicalOperator.NotEquals;
        stringEqualsSurveyQ.SetIsPositiveResult();
        Assert.IsFalse(stringEqualsSurveyQ.IsPositiveResult);

        var intEqualsSurveyQ = new IntSurveyAnswer
        {
            Question = new IntSurveyQuestion
            {
                QuestionText = "What is 1+1?",
                OptimalAnswer = 2,
                OptimalAnswerLogicalOp = LogicalOperator.Equals,
            },
            ValueGiven = 2
        };

        intEqualsSurveyQ.SetIsPositiveResult();
        Assert.IsTrue(intEqualsSurveyQ.IsPositiveResult);

        // Check a false value
        intEqualsSurveyQ.Question.OptimalAnswerLogicalOp = LogicalOperator.NotEquals;
        intEqualsSurveyQ.SetIsPositiveResult();
        Assert.IsFalse(intEqualsSurveyQ.IsPositiveResult);

        var intGTSurveyQ = new IntSurveyAnswer
        {
            Question = new IntSurveyQuestion
            {
                QuestionText = "What is greater than 1+1?",
                OptimalAnswer = 2,
                OptimalAnswerLogicalOp = LogicalOperator.GreaterThan,
            },
            ValueGiven = 4
        };
        intGTSurveyQ.SetIsPositiveResult();
        Assert.IsTrue(intGTSurveyQ.IsPositiveResult);

        // Check a false value
        intGTSurveyQ.Question.OptimalAnswerLogicalOp = LogicalOperator.LessThan;
        intGTSurveyQ.SetIsPositiveResult();
        Assert.IsFalse(intGTSurveyQ.IsPositiveResult);

        var intLTSurveyQ = new IntSurveyAnswer
        {
            Question = new IntSurveyQuestion
            {
                QuestionText = "What is less than 10+1?",
                OptimalAnswer = 10,
                OptimalAnswerLogicalOp = LogicalOperator.LessThan,
            },
            ValueGiven = 9
        };
        intLTSurveyQ.SetIsPositiveResult();
        Assert.IsTrue(intLTSurveyQ.IsPositiveResult);

        // Check a false value
        intLTSurveyQ.Question.OptimalAnswerLogicalOp = LogicalOperator.GreaterThan;
        intLTSurveyQ.SetIsPositiveResult();
        Assert.IsFalse(intLTSurveyQ.IsPositiveResult);

        var boolSurveyQ = new BooleanSurveyAnswer
        {
            Question = new BooleanSurveyQuestion
            {
                QuestionText = "Is this true?",
                OptimalAnswer = true,
                OptimalAnswerLogicalOp = LogicalOperator.Equals,
            },
            ValueGiven = true
        };
        boolSurveyQ.SetIsPositiveResult();
        Assert.IsTrue(boolSurveyQ.IsPositiveResult);

        // Check a false value
        boolSurveyQ.SetIsPositiveResult();
        boolSurveyQ.Question.OptimalAnswerLogicalOp = LogicalOperator.NotEquals;
        boolSurveyQ.SetIsPositiveResult();
        Assert.IsFalse(boolSurveyQ.IsPositiveResult);
    }
}
