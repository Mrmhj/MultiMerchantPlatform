using BuildingBlocks.Core.CQRS;
using RiskService.Application.Commands;
using RiskService.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace RiskService.Controllers;

/// <summary>
/// 风控内部接口 — 供其他微服务通过 X-Internal-Key 调用（事件上报 / 风控决策），不对外暴露。
/// 业务方集成点：下单、领券、登录失败、评价等关键操作后上报事件；下单/领券前调用决策接口。
/// </summary>
[ApiController]
[Route("api/risk/internal")]
[Produces("application/json")]
public sealed class InternalRiskController(IMediator mediator, IConfiguration configuration) : ControllerBase
{
    private readonly string _internalKey = configuration["Internal:Key"] ?? string.Empty;

    /// <summary>批量上报风控事件（落库 + 规则引擎实时评估，返回命中案例）</summary>
    /// <param name="request">事件列表</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 评估结果（Submitted/Hits/Cases/Blocked）</returns>
    /// <response code="200">评估结果</response>
    /// <response code="401">内部密钥错误</response>
    [HttpPost("events")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SubmitRiskEventResponse>> SubmitEvents(
        [FromBody] List<SubmitRiskEventRequest> request,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        return Ok(await mediator.SendAsync<SubmitRiskEventsCommand, SubmitRiskEventResponse>(
            new SubmitRiskEventsCommand(request ?? []), ct));
    }

    /// <summary>风控决策（业务方关键操作前调用，判断是否放行）</summary>
    /// <param name="request">决策请求（场景 + 用户/IP/设备）</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 决策结果（Allow/Reason/命中的黑名单或案例）</returns>
    /// <response code="200">决策结果</response>
    /// <response code="401">内部密钥错误</response>
    [HttpPost("decide")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RiskDecisionResponse>> Decide(
        [FromBody] RiskDecisionRequest request,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        return Ok(await mediator.SendAsync<RiskDecisionCommand, RiskDecisionResponse>(
            new RiskDecisionCommand(request), ct));
    }
}
