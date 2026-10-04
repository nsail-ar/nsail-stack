// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Scriban;
using Scriban.Runtime;

namespace NSail.SourceGenerator.Templating;

public class SourceTemplate
{
    readonly Template _template;
 
    internal SourceTemplate(Template template)
    {
        _template = template;
    }

    public string Render(object model)
    {
        var scriptObject = new ScriptObject();
        scriptObject.Import(model, renamer: member => member.Name);

        var templateContext = new TemplateContext
        {
            MemberRenamer = member => member.Name
        };
        templateContext.PushGlobal(scriptObject);

        return _template.Render(templateContext);
    } 
}