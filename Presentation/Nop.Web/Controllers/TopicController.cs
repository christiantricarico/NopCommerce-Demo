﻿using Microsoft.AspNetCore.Mvc;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Services.Topics;
using Nop.Web.Factories;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Web.Controllers;

[AutoValidateAntiforgeryToken]
public partial class TopicController : BasePublicController
{
    #region Fields

    protected readonly IAclService _aclService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IPermissionService _permissionService;
    protected readonly IStoreMappingService _storeMappingService;
    protected readonly ITopicModelFactory _topicModelFactory;
    protected readonly ITopicService _topicService;

    #endregion

    #region Ctor

    public TopicController(IAclService aclService,
        ILocalizationService localizationService,
        IPermissionService permissionService,
        IStoreMappingService storeMappingService,
        ITopicModelFactory topicModelFactory,
        ITopicService topicService)
    {
        _aclService = aclService;
        _localizationService = localizationService;
        _permissionService = permissionService;
        _storeMappingService = storeMappingService;
        _topicModelFactory = topicModelFactory;
        _topicService = topicService;
    }

    #endregion

    #region Methods

    public virtual async Task<IActionResult> TopicDetails(int topicId)
    {
        var topic = await _topicService.GetTopicByIdAsync(topicId);

        if (topic == null)
            return InvokeHttp404();

        var notAvailable = !topic.Published ||
                           //ACL (access control list)
                           !await _aclService.AuthorizeAsync(topic) ||
                           //store mapping
                           !await _storeMappingService.AuthorizeAsync(topic);

        //allow administrators to preview any topic
        var hasAdminAccess = await _permissionService.AuthorizeAsync(StandardPermission.Security.ACCESS_ADMIN_PANEL) && await _permissionService.AuthorizeAsync(StandardPermission.ContentManagement.TOPICS_VIEW);

        if (notAvailable && !hasAdminAccess)
            return InvokeHttp404();

        var model = await _topicModelFactory.PrepareTopicModelAsync(topic);

        //display "edit" (manage) link
        if (hasAdminAccess)
            DisplayEditLink(Url.Action("Edit", "Topic", new { id = model.Id, area = AreaNames.ADMIN }));

        //template
        var templateViewPath = await _topicModelFactory.PrepareTemplateViewPathAsync(model.TopicTemplateId);
        return View(templateViewPath, model);
    }

    [CheckLanguageSeoCode(ignore: true)]
    public virtual async Task<IActionResult> TopicDetailsPopup(string systemName)
    {
        var model = await _topicModelFactory.PrepareTopicModelBySystemNameAsync(systemName);
        if (model == null)
            return InvokeHttp404();

        ViewBag.IsPopup = true;

        //template
        var templateViewPath = await _topicModelFactory.PrepareTemplateViewPathAsync(model.TopicTemplateId);
        return PartialView(templateViewPath, model);
    }

    [HttpPost]
    public virtual async Task<IActionResult> Authenticate(int id, string password)
    {
        var authResult = false;
        var title = string.Empty;
        var body = string.Empty;
        var error = string.Empty;

        var topic = await _topicService.GetTopicByIdAsync(id);
        if (topic != null &&
            topic.Published &&
            //password protected?
            topic.IsPasswordProtected &&
            //store mapping
            await _storeMappingService.AuthorizeAsync(topic) &&
            //ACL (access control list)
            await _aclService.AuthorizeAsync(topic))
        {
            if (topic.Password != null && topic.Password.Equals(password))
            {
                authResult = true;
                title = await _localizationService.GetLocalizedAsync(topic, x => x.Title);
                body = await _localizationService.GetLocalizedAsync(topic, x => x.Body);
            }
            else
            {
                error = await _localizationService.GetResourceAsync("Topic.WrongPassword");
            }
        }

        return Json(new { Authenticated = authResult, Title = title, Body = body, Error = error });
    }

    #endregion
}