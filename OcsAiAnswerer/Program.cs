using System.ClientModel;
using System.Text.Json.Serialization;

using GenerativeAI.Microsoft;

using Microsoft.Extensions.AI;

using OpenAI;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});
builder.Services.AddAiProviders();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://*.chaoxing.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowedToAllowWildcardSubdomains();
    });
});

var app = builder.Build();

app.UseCors();

app.MapPost("/query", async (AnswerService answerService, QuestionModel query, CancellationToken ct) =>
{
    try
    {
        var answer = await answerService.SolveAsync(query.Title, query.Type, query.Options, ct);
        return new ResponseModel(query.Title, answer, null);
    }
    catch (TaskCanceledException)
    {
        throw;
    }
    catch (NoAnswerException)
    {
        return new ResponseModel(query.Title, null, "无可用的AI服务");
    }
    catch (Exception ex)
    {
        return new ResponseModel(query.Title, null, $"发生错误：${ex.GetType().Name}: {ex.Message}");
    }
});

app.Run();

#region Models

public record QuestionModel(string Title, string? Type, string? Options);

public record ResponseModel(string Question, string[]? Answer, string? Error);

#endregion

#region Services

internal class ChatClientProvider
{
    public List<IChatClient> Clients { get; init; } = [];
    
    public ChatClientProvider(IConfiguration config, ILogger<ChatClientProvider> logger)
    {       
        var section = config.GetSection("AiProviders");
        foreach (var child in section.GetChildren())
        {
            var type = child["Type"];
            var client = ChatClientFactory.Build(type, child);
            if (client is null) continue;
            Clients.Add(client);
            logger.LogInformation("Chat client {Client} initialized with type {Type}", client.GetType().Name, type);
        }
    }
}

internal class AnswerService(ChatClientProvider chatClient, ILogger<AnswerService> logger)
{
    private const string SystemPrompt = """
                                        You are a helpful AI assistant. You are powerful, intelligent, all-knowing, and highly proficient in all areas of knowledge. The user will present you with questions they encounter during their studies. Your task is to help them solve these questions accurately. The user will usually provide the **question** and its **type**, such as `single`, `multiple`, `judgement`, or `completion`. Please follow these rules:
                                        
                                         1. **Single Choice / Judgement**: Output only the full text of the correct option (one line).
                                         2. **Multiple Choice**: Output the full text of all correct options, one per line.
                                         3. **Completion (e.g., with blanks like `___`)**: Output only the text(s) to be filled in, one line per blank, in order.
                                        
                                        Your responses should be concise and directly usable by the user. Do not provide any extra explanation unless the user explicitly asks for it.
                                        """;

    private readonly ChatOptions _chatOptions = new()
    {
        MaxOutputTokens = 2048,
        AllowMultipleToolCalls = false,
        ResponseFormat = ChatResponseFormat.Text
    };
    
    public async Task<string[]> SolveAsync(string question, string? type, string? options, CancellationToken ct)
    {
        var user = $"Here is my question:\n{question} (Type: {type}){(string.IsNullOrEmpty(options) ? "(No options)" : "\n" + options)}";
        ChatMessage[] chats =
        [
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, user)
        ];
        
        foreach (var client in chatClient.Clients)
        {
            try
            {
                logger.LogDebug("Processing {Type} question {Title} with client {Client}", type, question, client.GetType().Name);
                var response = await client.GetResponseAsync(chats, _chatOptions, ct);
                if (response.FinishReason == null || response.FinishReason.Value != ChatFinishReason.Stop)
                {
                    logger.LogWarning("Chat client {Client} did not finish properly: {Reason}, Text: {Text}",
                        client.GetType().Name, response.FinishReason, response.Text);
                    continue;
                }
                logger.LogDebug("Chat client {Client} finished successfully with response: {Text}", client.GetType().Name, response.Text);

                return response.Text.Split('\n', options: StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while processing question with client {Client}, try next ...", client.GetType().Name);
            }
            
        }

        throw new NoAnswerException();
    }
}

#endregion

#region Utils

internal static class ServiceCollectionExtension
{
    public static IServiceCollection AddAiProviders(this IServiceCollection services)
    {
        services.AddSingleton<ChatClientProvider>();
        services.AddSingleton<AnswerService>();

        return services;
    }
}

internal static class ChatClientFactory
{
    public static IChatClient? Build(string? type, IConfigurationSection config)
    {
        return type switch
        {
            "GoogleAi" => BuildGoogleAi(config),
            "OpenAi" => BuildOpenAi(config),
            _ => null
        };
    }

    private static GenerativeAIChatClient? BuildGoogleAi(IConfigurationSection config)
    {
        const string defaultModel = "gemini-2.0-flash";
        
        if (config["ApiKey"] is not { } key) return null;
        var model = config["Model"] ?? defaultModel;
        
        return new GenerativeAIChatClient(key, model, false);
    }

    private static IChatClient? BuildOpenAi(IConfigurationSection config)
    {
        if (config["ApiKey"] is not { } key) return null;
        if (config["Model"] is not { } model) return null;
        var endpoint = config["Endpoint"];

        var options = new OpenAIClientOptions();
        if (!string.IsNullOrEmpty(endpoint))
            options.Endpoint = new Uri(endpoint);

        return new OpenAI.Chat.ChatClient(model, new ApiKeyCredential(key), options).AsIChatClient();
    }
}

[JsonSerializable(typeof(QuestionModel))]
[JsonSerializable(typeof(ResponseModel))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}

#endregion

internal class NoAnswerException : Exception
{
}