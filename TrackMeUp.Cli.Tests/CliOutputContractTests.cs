// SPDX-License-Identifier: MIT

using System.Linq;
using System.Text.Json;
using TrackMeUp.Application;
using TrackMeUp.Cli;
using Xunit;

namespace TrackMeUp.Cli.Tests;

public sealed class CliOutputContractTests
{
    [Theory]
    [InlineData("en-US", "Windows language", "System", "Light", "Dark")]
    [InlineData("it-IT", "Lingua di Windows", "Sistema", "Chiaro", "Scuro")]
    [InlineData("fr-FR", "Langue de Windows", "Système", "Clair", "Sombre")]
    [InlineData("de-DE", "Windows-Sprache", "System", "Hell", "Dunkel")]
    [InlineData("es-ES", "Idioma de Windows", "Sistema", "Claro", "Oscuro")]
    [InlineData("zh-Hans", "Windows 语言", "系统", "浅色", "深色")]
    [InlineData("vi-VN", "Ngôn ngữ Windows", "Hệ thống", "Sáng", "Tối")]
    [InlineData("ko-KR", "Windows 언어", "시스템", "밝게", "어둡게")]
    [InlineData("pt-PT", "Idioma do Windows", "Sistema", "Claro", "Escuro")]
    [InlineData("pt-BR", "Idioma do Windows", "Sistema", "Claro", "Escuro")]
    public void WizardChoices_LocalizeLabelsAndPreserveContractValues(
        string locale,
        string languageSystem,
        string themeSystem,
        string themeLight,
        string themeDark)
    {
        var output = new CliOutput(new CliOptions(
            CliFormat.Plain,
            locale,
            false,
            false,
            5,
            false,
            []));

        var languages = CliRouter.LanguageWizardChoices(output);
        var themes = CliRouter.ThemeWizardChoices(output);

        Assert.Equal(CliOptions.SupportedLanguages, languages.Select(choice => choice.Value));
        Assert.Equal(languageSystem, languages[0].Label);
        Assert.Equal(["system", "light", "dark"], themes.Select(choice => choice.Value));
        Assert.Equal([themeSystem, themeLight, themeDark], themes.Select(choice => choice.Label));
    }

    [Theory]
    [InlineData("en-US", "Operation completed.", "The operation could not be completed.")]
    [InlineData("it-IT", "Operazione completata.", "Non è stato possibile completare l’operazione.")]
    [InlineData("zh-Hans", "操作已完成。", "无法完成操作。")]
    [InlineData("pt-BR", "Operação concluída.", "Não foi possível concluir a operação.")]
    public void HumanResultFallback_NeverPrintsAnUntranslatedMessageKey(
        string locale,
        string expectedSuccess,
        string expectedFailure)
    {
        var output = new CliOutput(new CliOptions(
            CliFormat.Plain,
            locale,
            false,
            false,
            5,
            false,
            []));

        Assert.Equal(expectedSuccess, output.ResultText("UnknownSuccessMessageKey", succeeded: true));
        Assert.Equal(expectedFailure, output.ResultText("UnknownFailureMessageKey", succeeded: false));
    }

    [Fact]
    public void JsonResultEnvelope_KeepsStableEnglishFieldNamesAndCodes()
    {
        var result = OperationResult<object>.Failure(
            "command.arguments.invalid",
            "CommandInvalid",
            new ValidationIssue("language", "unsupported", "CommandInvalid"));

        using var document = JsonDocument.Parse(CliOutput.SerializeResult(result));

        Assert.Equal(["succeeded", "code", "messageKey", "value", "issues"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal("command.arguments.invalid", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("CommandInvalid", document.RootElement.GetProperty("messageKey").GetString());
        Assert.Equal("language", document.RootElement.GetProperty("issues")[0].GetProperty("field").GetString());
    }
}
