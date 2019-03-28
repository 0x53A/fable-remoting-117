open System
open System.IO
open System.Threading.Tasks

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection

open FSharp.Control.Tasks.V2
open Giraffe
open Shared

open Fable.Remoting.Server
open Fable.Remoting.Giraffe

let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x

let publicPath = Path.GetFullPath "../Client/public"
let port = "SERVER_PORT" |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us

let getInitCounter () : Task<Counter> = task { return { Value = 42 } }

let getBlobs() =
    let dir = Path.Combine(__SOURCE_DIRECTORY__, "blobs")
    let files = Directory.GetFiles(dir)

    [|
        yield! BitConverter.GetBytes(files.Length)
        for f in files do
            let id =
                f
                |> Path.GetFileNameWithoutExtension
                |> int
            yield! BitConverter.GetBytes(id)
            yield! File.ReadAllBytes(f)
    |]

let counterApi = {
    initialCounter = getInitCounter >> Async.AwaitTask
    getBlobs = async {
        let blob = getBlobs()
        printfn "[Fast] returning blob with length %i" blob.Length
        return blob
      }
    getBlobsSlow = async {
        let blob = getBlobs()
        printfn "[Slow] returning blob with length %i" blob.Length
        return Some (blob)
      }
}

let webApp =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue counterApi
    |> Remoting.buildHttpHandler


let configureApp (app : IApplicationBuilder) =
    app.UseDefaultFiles()
       .UseStaticFiles()
       .UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore

WebHost
    .CreateDefaultBuilder()
    .UseWebRoot(publicPath)
    .UseContentRoot(publicPath)
    .Configure(Action<IApplicationBuilder> configureApp)
    .ConfigureServices(configureServices)
    .UseUrls("http://0.0.0.0:" + port.ToString() + "/")
    .Build()
    .Run()