// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGenerator.Generation.Http.Models;

public class ParameterClassModel
{
    public string Namespace { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string Declaration
    {
        get
        {
            if (string.IsNullOrEmpty(Namespace))
                return ClassName;
            else
                return $"{Namespace}.{ClassName}";
        }
    }

    public List<ParameterPropertyModel> Properties { get; set; } = new();
}
