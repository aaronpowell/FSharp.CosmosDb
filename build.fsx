open System.IO.Compression
#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.BuildServer

Target.initEnvironment ()

let dotnetVersion = "net6.0"

let sln = "./FSharp.CosmosDb.sln"
let nupkgPath = "./.nupkg"

let getChangelog () =
    let changelog = "CHANGELOG.md" |> Changelog.load
    changelog.LatestEntry

let isRelease (targets: Target list) =
    targets
    |> Seq.map (fun t -> t.Name)
    |> Seq.exists ((=) "Release")

let configuration (targets: Target list) =
    let defaultVal =
        if isRelease targets then
            "Release"
        else
            "Debug"

    match Environment.environVarOrDefault "CONFIGURATION" defaultVal with
    | "Debug" -> DotNet.BuildConfiguration.Debug
    | "Release" -> DotNet.BuildConfiguration.Release
    | config -> DotNet.BuildConfiguration.Custom config

let getVersionNumber (changeLog: Changelog.ChangelogEntry) (targets: Target list) =
    match GitHubActions.Environment.CI false, isRelease targets with
    | (true, true) -> changeLog.NuGetVersion
    | (true, false) -> sprintf "%s-ci-%s" changeLog.NuGetVersion GitHubActions.Environment.RunId
    | (_, _) -> sprintf "%s-local" changeLog.NuGetVersion

Target.create "Clean" (fun _ ->
    DotNet.exec id "clean" "" |> ignore
    !!nupkgPath |> Shell.cleanDirs)

Target.create "Restore" (fun _ -> DotNet.restore id sln)

Target.create "Build" (fun ctx ->
    let changelog = getChangelog ()

    let args =
        [ sprintf "/p:PackageVersion=%s" (getVersionNumber changelog (ctx.Context.AllExecutingTargets))
          "--no-restore" ]

    DotNet.build
        (fun c ->
            { c with
                Configuration = configuration (ctx.Context.AllExecutingTargets)
                Common = c.Common |> DotNet.Options.withAdditionalArgs args })
        sln)

Target.create "Publish" (fun ctx ->
    let changelog = getChangelog ()

    let args =
        [ sprintf "/p:PackageVersion=%s" (getVersionNumber changelog (ctx.Context.AllExecutingTargets))
          "--no-restore"
          "--no-build" ]

    DotNet.publish
        (fun c ->
            { c with
                Configuration = configuration (ctx.Context.AllExecutingTargets)
                Common = c.Common |> DotNet.Options.withAdditionalArgs args })
        sln)

Target.create "Package" (fun ctx ->
    let changelog = getChangelog ()

    let version =
        ctx.Context.AllExecutingTargets
        |> getVersionNumber changelog

    let args =
        [ sprintf "/p:PackageVersion=%s" version
          sprintf "/p:PackageReleaseNotes=\"%s\"" (sprintf "%O" changelog) ]

    DotNet.pack
        (fun c ->
            { c with
                Configuration = configuration (ctx.Context.AllExecutingTargets)
                OutputPath = Some nupkgPath
                Common = c.Common |> DotNet.Options.withAdditionalArgs args })
        sln

    let analyzerNupkg =
        nupkgPath
        </> (sprintf "FSharp.CosmosDb.Analyzer.%s.nupkg" version)

    Zip.unzip (nupkgPath </> "analyzerNupkgContents") analyzerNupkg

    Directory.ensure (
        nupkgPath
        </> "analyzer"
        </> "lib"
        </> dotnetVersion
    )

    Shell.copy
        (nupkgPath </> "analyzer")
        [ (nupkgPath
           </> "analyzerNupkgContents"
           </> "FSharp.CosmosDb.Analyzer.nuspec") ]

    let buildConfig =
        if isRelease ctx.Context.AllExecutingTargets then
            "Release"
        else
            "Debug"

    Shell.copyDir
        (nupkgPath
         </> "analyzer"
         </> "lib"
         </> dotnetVersion)
        ("src"
         </> "FSharp.CosmosDb.Analyzer"
         </> "bin"
         </> buildConfig
         </> dotnetVersion
         </> "publish")
        (fun _ -> true)

    File.delete analyzerNupkg
    ZipFile.CreateFromDirectory((nupkgPath </> "analyzer"), analyzerNupkg))

Target.create "PackageVersion" (fun _ ->
    let version = getChangelog ()
    printfn "The version is %s" version.NuGetVersion)

Target.create "Changelog" (fun _ ->
    let changelog = getChangelog ()
    Directory.ensure nupkgPath

    [| sprintf "%O" changelog |]
    |> File.append (nupkgPath </> "changelog.md"))

Target.create "Test" (fun _ ->
    DotNet.exec
        (fun p -> { p with Timeout = Some(System.TimeSpan.FromMinutes 10.) })
        "run"
        """--project "./tests/FSharp.CosmosDb.Analyzer.Tests/FSharp.CosmosDb.Analyzer.Tests.fsproj" -- --fail-on-focused-tests --debug --summary"""
    |> fun r ->
        if not r.OK then
            failwithf "Errors while running LSP tests:\n%s" (r.Errors |> String.concat "\n\t"))

Target.create "RunAnalyzer" (fun ctx ->
    let args =
        sprintf
            "--project samples/FSharp.CosmosDb.Samples/FSharp.CosmosDb.Samples.fsproj --analyzers-path src/FSharp.CosmosDb.Analyzer/bin/%A/%s/publish --verbose"
            (configuration (ctx.Context.AllExecutingTargets))
            dotnetVersion

    DotNet.exec id "fsharp-analyzers" args |> ignore)

Target.create "Default" ignore
Target.create "Release" ignore
Target.create "CI" ignore

"Clean" ==> "Restore" ==> "Build" ==> "Default"

"Default"
==> "Publish"
==> "Test"
==> "Package"
==> "Changelog"
==> "Release"

"Default"
==> "Publish"
==> "Test"
==> "Package"
==> "Changelog"
==> "CI"

"Default" ==> "Publish" ==> "RunAnalyzer"

Target.runOrDefault "Default"
