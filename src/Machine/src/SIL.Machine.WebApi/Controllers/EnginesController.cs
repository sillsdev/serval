﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SIL.Machine.Annotations;
using SIL.Machine.Translation;
using SIL.Machine.WebApi.DataAccess;
using SIL.Machine.WebApi.Models;
using SIL.Machine.WebApi.Services;

namespace SIL.Machine.WebApi.Controllers
{
	[Area("Translation")]
	[Route("[area]/[controller]", Name = RouteNames.Engines)]
	public class EnginesController : Controller
	{
		private readonly IAuthorizationService _authService;
		private readonly IEngineRepository _engines;
		private readonly IEngineService _engineService;

		public EnginesController(IAuthorizationService authService, IEngineRepository engines,
			IEngineService engineService)
		{
			_authService = authService;
			_engines = engines;
			_engineService = engineService;
		}

		[HttpGet]
		public async Task<IEnumerable<EngineDto>> GetAllAsync()
		{
			var engines = new List<EngineDto>();
			foreach (Engine engine in await _engines.GetAllAsync())
			{
				if (await AuthorizeAsync(engine, Operations.Read))
					engines.Add(CreateDto(engine));
			}
			return engines;
		}

		[HttpGet("{locatorType}:{locator}")]
		public async Task<IActionResult> GetAsync(string locatorType, string locator)
		{
			Engine engine = await _engines.GetByLocatorAsync(GetLocatorType(locatorType), locator);
			if (engine == null)
				return NotFound();
			if (!await AuthorizeAsync(engine, Operations.Read))
				return StatusCode(StatusCodes.Status403Forbidden);

			return Ok(CreateDto(engine));
		}

		[HttpPost("{locatorType}:{locator}/actions/translate")]
		public async Task<IActionResult> TranslateAsync(string locatorType, string locator, [FromBody] string[] segment)
		{
			Engine engine = await _engines.GetByLocatorAsync(GetLocatorType(locatorType), locator);
			if (engine == null)
				return NotFound();
			if (!await AuthorizeAsync(engine, Operations.Read))
				return StatusCode(StatusCodes.Status403Forbidden);

			TranslationResult result = await _engineService.TranslateAsync(engine.Id, segment);
			if (result == null)
				return NotFound();
			return Ok(CreateDto(result, segment));
		}

		[HttpPost("{locatorType}:{locator}/actions/translate/{n}")]
		public async Task<IActionResult> TranslateAsync(string locatorType, string locator, int n,
			[FromBody] string[] segment)
		{
			Engine engine = await _engines.GetByLocatorAsync(GetLocatorType(locatorType), locator);
			if (engine == null)
				return NotFound();
			if (!await AuthorizeAsync(engine, Operations.Read))
				return StatusCode(StatusCodes.Status403Forbidden);

			IEnumerable<TranslationResult> results = await _engineService.TranslateAsync(engine.Id, n, segment);
			if (results == null)
				return NotFound();
			return Ok(results.Select(tr => CreateDto(tr, segment)));
		}

		[HttpPost("{locatorType}:{locator}/actions/interactiveTranslate")]
		public async Task<IActionResult> InteractiveTranslateAsync(string locatorType, string locator,
			[FromBody] string[] segment)
		{
			Engine engine = await _engines.GetByLocatorAsync(GetLocatorType(locatorType), locator);
			if (engine == null)
				return NotFound();
			if (!await AuthorizeAsync(engine, Operations.Read))
				return StatusCode(StatusCodes.Status403Forbidden);

			HybridInteractiveTranslationResult result = await _engineService.InteractiveTranslateAsync(engine.Id,
				segment);
			if (result == null)
				return NotFound();
			return Ok(CreateDto(result, segment));
		}

		[HttpPost("{locatorType}:{locator}/actions/trainSegment")]
		public async Task<IActionResult> TrainSegmentAsync(string locatorType, string locator,
			[FromBody] SegmentPairDto segmentPair)
		{
			Engine engine = await _engines.GetByLocatorAsync(GetLocatorType(locatorType), locator);
			if (engine == null)
				return NotFound();
			if (!await AuthorizeAsync(engine, Operations.Update))
				return StatusCode(StatusCodes.Status403Forbidden);

			if (!await _engineService.TrainSegmentAsync(engine.Id, segmentPair.SourceSegment,
				segmentPair.TargetSegment))
			{
				return NotFound();
			}
			return Ok();
		}

		private async Task<bool> AuthorizeAsync(Engine engine, OperationAuthorizationRequirement operation)
		{
			AuthorizationResult result = await _authService.AuthorizeAsync(User, engine, operation);
			return result.Succeeded;
		}

		private static EngineLocatorType GetLocatorType(string type)
		{
			switch (type)
			{
				case "id":
					return EngineLocatorType.Id;
				case "langTag":
					return EngineLocatorType.LanguageTag;
				case "project":
					return EngineLocatorType.Project;
			}
			return EngineLocatorType.Id;
		}

		private static TranslationResultDto CreateDto(TranslationResult result, IReadOnlyList<string> sourceSegment)
		{
			if (result == null)
				return null;

			return new TranslationResultDto
			{
				Target = Enumerable.Range(0, result.TargetSegment.Count)
					.Select(j => result.RecaseTargetWord(sourceSegment, j)).ToArray(),
				Confidences = result.WordConfidences.Select(c => (float) c).ToArray(),
				Sources = result.WordSources.ToArray(),
				Alignment = CreateDto(result.Alignment),
				Phrases = result.Phrases.Select(CreateDto).ToArray()
			};
		}

		private static WordGraphDto CreateDto(WordGraph wordGraph, IReadOnlyList<string> sourceSegment)
		{
			return new WordGraphDto
			{
				InitialStateScore = (float) wordGraph.InitialStateScore,
				FinalStates = wordGraph.FinalStates.ToArray(),
				Arcs = wordGraph.Arcs.Select(a => CreateDto(a, sourceSegment)).ToArray()
			};
		}

		private static WordGraphArcDto CreateDto(WordGraphArc arc, IReadOnlyList<string> sourceSegment)
		{
			return new WordGraphArcDto
			{
				PrevState = arc.PrevState,
				NextState = arc.NextState,
				Score = (float) arc.Score,
				Words = Enumerable.Range(0, arc.Words.Count)
					.Select(j =>
						arc.Alignment.RecaseTargetWord(sourceSegment, arc.SourceSegmentRange.Start, arc.Words, j))
					.ToArray(),
				Confidences = arc.WordConfidences.Select(c => (float) c).ToArray(),
				SourceSegmentRange = CreateDto(arc.SourceSegmentRange),
				IsUnknown = arc.IsUnknown,
				Alignment = CreateDto(arc.Alignment)
			};
		}

		private static AlignedWordPairDto[] CreateDto(WordAlignmentMatrix matrix)
		{
			var wordPairs = new List<AlignedWordPairDto>();
			for (int i = 0; i < matrix.RowCount; i++)
			{
				for (int j = 0; j < matrix.ColumnCount; j++)
				{
					if (matrix[i, j])
						wordPairs.Add(new AlignedWordPairDto { SourceIndex = i, TargetIndex = j });
				}
			}
			return wordPairs.ToArray();
		}

		private EngineDto CreateDto(Engine engine)
		{
			return new EngineDto
			{
				Id = engine.Id,
				Href = Url.GetEntityUrl(RouteNames.Engines, engine.Id),
				SourceLanguageTag = engine.SourceLanguageTag,
				TargetLanguageTag = engine.TargetLanguageTag,
				IsShared = engine.IsShared,
				Projects = engine.Projects.Select(projectId =>
					Url.CreateLinkDto(RouteNames.Projects, projectId)).ToArray(),
				Confidence = engine.Confidence,
				TrainedSegmentCount = engine.TrainedSegmentCount
			};
		}

		private static RangeDto CreateDto(Range<int> range)
		{
			return new RangeDto()
			{
				Start = range.Start,
				End = range.End
			};
		}

		private static InteractiveTranslationResultDto CreateDto(HybridInteractiveTranslationResult result,
			IReadOnlyList<string> sourceSegment)
		{
			return new InteractiveTranslationResultDto
			{
				WordGraph = CreateDto(result.SmtWordGraph, sourceSegment),
				RuleResult = CreateDto(result.RuleResult, sourceSegment)
			};
		}

		private static PhraseDto CreateDto(Phrase phrase)
		{
			return new PhraseDto
			{
				SourceSegmentRange = CreateDto(phrase.SourceSegmentRange),
				TargetSegmentCut = phrase.TargetSegmentCut,
				Confidence = phrase.Confidence
			};
		}
	}
}
