using Common.Engine;
using Common.Engine.Surveys;
using Common.Engine.Surveys.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Server.Controllers;

/// <summary>
/// Handles updates to survey questions
/// </summary>
[Authorize]
[Route("api/[controller]")]
[ApiController]
public class SurveyQuestionsController(ILogger<SurveyQuestionsController> logger, ISurveyManagerDataLoader surveyManagerDataLoader) : ControllerBase
{
    /// <summary>
    /// Load survey pages
    /// </summary>
    // GET: api/SurveyQuestions
    [HttpGet]
    public async Task<List<SurveyPageDTO>> SurveyQuestions()
    {
        logger.LogInformation("Called GetSurveyQuestions");
        var allPages = await surveyManagerDataLoader.GetSurveyPages(false);
        return allPages.Select(p => new SurveyPageDTO(p)).ToList();
    }

    /// <summary>
    /// Save a survey page + questions
    /// </summary>
    // POST: api/SurveyQuestions
    [HttpPost]
    public async Task<List<SurveyPageDTO>> SavePage([FromBody] SurveyPageDTO pageUpdate)
    {
        logger.LogInformation("Called SavePage");

        await surveyManagerDataLoader.SaveSurveyPage(pageUpdate);

        return await SurveyQuestions();
    }

    // POST: api/SurveyQuestions/DeletePage
    [HttpPost(nameof(DeletePage))]
    public async Task<IActionResult> DeletePage([FromBody] BaseDTO pageToDelete)
    {
        logger.LogInformation("Called DeletePage");

        var success = await surveyManagerDataLoader.DeleteSurveyPage(int.Parse(pageToDelete.Id!));
        if (success)
        {
            return Accepted();
        }
        else
        {
            logger.LogWarning("Page not found for delete");
            return NotFound();
        }
    }
}
