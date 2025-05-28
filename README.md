# OcsAiAnswerer

## 简介

OcsAiAnswerer 是一个可以和[ocsjs](https://github.com/ocsjs/ocsjs)配合的，基于多种 AI 大模型的答题辅助服务，支持 Google Gemini、OpenAI、DeepSeek 等主流 AI 平台。用户可通过简单配置，快速接入多种 AI 服务，实现自动化答题、智能解答等功能。

## 安装教程

### Windows

1. 前往 [Release 页面](https://github.com/dogdie233/OcsAiAnswerer/releases) 下载最新的 `OcsAiAnswerer-win64-aot.zip`。
2. 解压到任意目录。

### Linux

1. 安装最新的 [.NET Runtime](https://dotnet.microsoft.com/zh-cn/download/dotnet)（必须 9.0 及以上）。
2. 前往 [Release 页面](https://github.com/dogdie233/OcsAiAnswerer/releases) 下载最新的 `OcsAiAnswerer-any.zip`。
3. 解压到任意目录。

## 使用教程

1. 解压后，找到并编辑 `appsettings.json` 文件（用记事本打开），配置你的 AI 平台密钥和参数（详见下表）。
2. 运行程序：
   - Windows：双击 `OcsAiAnswerer.exe` 或在命令行运行
   - Linux：在终端运行 `dotnet OcsAiAnswerer.dll`
3. 如果你是第一次用这个程序，还需要配置ocsjs的题库，依次在ocsjs的面板点击 `通用` -> `全局设置` -> `题库配置` 填入配置（配置参考下面，一般来说可以直接复制）
4. 完成后ocsjs将会使用本程序作为题库搜索题目

## `ocsjs` 参考配置

```json
[
    {
        "name": "Localhost OcsAiAnswerer",
        "homepage": "https://github.com/dogdie233/OciAiAnswerer",
        "url": "http://localhost:5000/query",
        "method": "post",
        "type": "fetch",
        "contentType": "json",
        "data": {
            "title": "${title}",
            "options": "${options}",
            "type": "${type}"
        },
        "headers": {
            "Content-Type": "application/json"
        },
        "handler": "return (res)=>res.error ? [res.question, undefined] : [res.question, res.answers.join('###')]"
    }
]
```

## `appsettings.json` 配置说明

`AiProviders` 字段用于配置可用的 AI 服务提供者。你可以配置多个，系统会自动按顺序尝试。

| Type      | 必填参数         | 可选参数   | 说明                       |
|-----------|------------------|------------|----------------------------|
| GoogleAi  | ApiKey           | Model      | Google Gemini，Model 默认为 `gemini-2.0-flash` |
| OpenAi    | ApiKey, Model    | Endpoint   | OpenAI，Endpoint 可自定义 API 地址（如 Azure OpenAI） |

**示例：**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AiProviders": [
    {
      "Type": "GoogleAi",
      "ApiKey": "你的GoogleApiKey",
      "Model": "gemini-2.0-pro"
    },
    {
      "Type": "OpenAi",
      "ApiKey": "你的OpenAiApiKey",
      "Model": "gpt-3.5-turbo",
      "Endpoint": "https://api.openai.com/v1"
    }
  ]
}
```

## 常见问题

- 支持多 AI 服务自动切换，优先尝试第一个，若不可用会自动尝试下一个直到没有Ai可用为止。
- 配置项修改后无需重启，直接生效（如遇异常请重启服务）。

> Q: 怎么添加deepseek？

A: appsettings.json中 `AiProviders` 中使用如下配置
```json
{
    "Type": "OpenAi",
    "ApiKey": "你的Deepseek ApiKey",
    "Model": "deepseek-chat",
    "Endpoint": "https://api.deepseek.com"
}
```

> Q: 启动程序之后显示下面的内容然后程序自动退出了  
> System.IO.IOException: Failed to bind to address http://127.0.0.1:5000: address already in use.  
>       ---> Microsoft.AspNetCore.Connections.AddressInUseException: Only one usage of each socket address (protocol/network address/port) is normally permitted.

A: 这是因为 `5000` 端口被占用了，搜一下怎么杀死占用5000端口的程序，或者修改本程序的监听端口，方法如下：

 1. 打开 `appsettings.json`，在 `AiProviders` 那一行的上面添加一行 `"urls": "http://127.0.0.1:5233",` 这里的`5233`可以换成任何端口
 2. `ocsjs` 配置中 `url` 那一栏也要一样改成一样的（记得加query）  
   
在 `appsettings.json` 中：
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "urls": "http://127.0.0.1:5233",
  "AiProviders": [
    ...
  ]
}
```

在 `ocsjs` 题库配置中：
```json
[
    {
        "name": "Localhost OcsAiAnswerer",
        "homepage": "https://github.com/dogdie233/OciAiAnswerer",
        "url": "http://localhost:5233/query",
        "method": "post",
        ...
    }
]
```

## 我不想把ApiKey写在配置文件里，可能会有泄露风险

你还可以把`ApiKey`写在`环境变量`里，例如原本的
```json
{
  "AiProviders": [
    {
      "Type": "GoogleAi",
      "ApiKey": "114514"
    }
  ]
}
```
可以把ApiKey删掉，然后在环境变量里填写`AiProviders__0__ApiKey=114514`这里的`0`是`index`
