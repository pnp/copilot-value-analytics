﻿using Entities.DB.Entities.AuditLog;
using System.ComponentModel.DataAnnotations.Schema;

namespace Entities.DB.Entities;

[Table("survey_general_responses")]
public class SurveyGeneralResponseDB : UserRelatedEntity
{
    [Column("responded")]
    public DateTime? Responded { get; set; }

    [Column("requested")]
    public DateTime Requested { get; set; }

    [Column("overrall_rating")]
    public int OverrallRating { get; set; }


    [ForeignKey(nameof(RelatedEvent))]
    [Column("related_audit_event_id")]
    public Guid? RelatedEventId { get; set; }
    public CommonAuditEvent? RelatedEvent { get; set; } = null;
}

/// <summary>
/// Associate a survey response with an activity type
/// </summary>
[Table("survey_response_activity_types")]
public class UserSurveyResponseActivityType : AbstractEFEntity
{

    [ForeignKey(nameof(UserSurveyResponse))]
    [Column("user_response_id")]
    public int UserSurveyResponseId { get; set; }
    public SurveyGeneralResponseDB UserSurveyResponse { get; set; } = null!;


    [ForeignKey(nameof(CopilotActivity))]
    [Column("copilot_activity_id")]
    public int CopilotActivityId { get; set; }
    public CopilotActivity CopilotActivity { get; set; } = null!;
}


[Table("survey_pages")]
public class SurveyPageDB : AbstractEFEntityWithName
{
    [Column("is_published")]
    public bool IsPublished { get; set; }
    public List<SurveyQuestionDefinitionDB> Questions { get; set; } = new();

    [Column("index")]
    public int PageIndex { get; set; }

    [Column("template_json")]
    public string AdaptiveCardTemplateJson { get; set; } = null!;

    [Column("deleted_utc")]
    public DateTime? DeletedUtc { get; set; }
}

[Table("survey_question_definitions")]
public class SurveyQuestionDefinitionDB : AbstractEFEntity
{
    [ForeignKey(nameof(SurveyPageDB))]
    [Column("for_SurveyPage_id")]
    public int ForSurveyPageId { get; set; }
    public SurveyPageDB ForSurveyPage { get; set; } = null!;

    /// <summary>
    /// For identifying specific questions in the survey
    /// </summary>
    [Column("question_id")]
    public string? QuestionId { get; set; }

    [Column("question")]
    public required string QuestionText { get; set; }

    [Column("optimal_answer_value")]
    public string? OptimalAnswerValue { get; set; } = null;

    [Column("optimal_answer_logical_op")]
    public LogicalOperator? OptimalAnswerLogicalOp { get; set; }

    [Column("data_type")]
    public required QuestionDatatype DataType { get; set; }

    [Column("index")]
    public int Index { get; set; }
}

[Table("survey_question_responses")]
public class SurveyQuestionResponseDB : UserRelatedEntity
{
    [ForeignKey(nameof(ForQuestion))]
    [Column("for_question_id")]
    public int ForQuestionId { get; set; }
    public SurveyQuestionDefinitionDB ForQuestion { get; set; } = null!;

    [Column("given_answer")]
    public required string GivenAnswer { get; set; }

    [Column("timestamp_utc")]
    public DateTime TimestampUtc { get; set; }

    [ForeignKey(nameof(ParentSurvey))]
    [Column("parent_survey_response_id")]
    public int ParentSurveyId { get; set; }
    public SurveyGeneralResponseDB ParentSurvey { get; set; } = null!;
}


public enum QuestionDatatype
{
    Unknown,
    String,
    Int,
    Bool,
}

public enum LogicalOperator
{
    Unknown = 0,
    Equals = 1,
    NotEquals = 2,
    GreaterThan = 3,
    LessThan = 4,
}
