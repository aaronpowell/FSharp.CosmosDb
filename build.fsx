#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

Target.initEnvironment()

let sln = "./FSharp.CosmosDb.sln"

let getChangelog() =
    let changelog = "CHANGELOG.md" |> Changelog.load
    changelog.LatestEntry

let isRelease (targets: Target list) =
    targets
    |> Seq.map (fun t -> t.Name)
    |> Seq.exists ((=) "Release")

let configuration (targets: Target list) =
    let defaultVal =
        if isRelease targets then "Release" else "Debug"
    match Environment.environVarOrDefault "CONFIGURATION" defaultVal with
    | "Debug" -> DotNet.BuildConfiguration.Debug
    | "Release" -> DotNet.BuildConfiguration.Release
    | config -> DotNet.BuildConfiguration.Custom config

Target.create "Clean" (fun _ ->
    DotNet.exec id "clean" "" |> ignore
    !!"./.nuget" |> Shell.cleanDirs)

Target.create "Restore" (fun _ -> DotNet.restore id sln)

Target.create "Build" (fun ctx ->
    let changelog = getChangelog()

    let args =
        [ sprintf "/p:PackageVersion=%s" (changelog.NuGetVersion)
          "--no-restore" ]
    DotNet.build (fun c ->
        { c with
              Configuration = configuration (ctx.Context.AllExecutingTargets)
              Common = c.Common |> DotNet.Options.withAdditionalArgs args }) sln)

Target.create "Publish" (fun ctx ->
    let changelog = getChangelog()

    let args =
        [ sprintf "/p:PackageVersion=%s" (changelog.NuGetVersion)
          "--no-restore"
          "--no-build" ]
    DotNet.publish (fun c ->
        { c with
              Configuration = configuration (ctx.Context.AllExecutingTargets)
              Common = c.Common |> DotNet.Options.withAdditionalArgs args }) sln)

Target.create "Package" (fun ctx ->
    let changelog = getChangelog()

    let args =
        [ sprintf "/p:PackageVersion=%s" (changelog.NuGetVersion)
          sprintf "/p:PackageReleaseNotes=\"%s\"" (sprintf "%O" changelog) ]
    DotNet.pack (fun c ->
        { c with
              Configuration = configuration (ctx.Context.AllExecutingTargets)
              Common = c.Common |> DotNet.Options.withAdditionalArgs args }) sln)

Target.create "PackageVersion" (fun _ ->
    let version = getChangelog()
    printfn "The version is %s" version.NuGetVersion)

Target.create "Changelog" (fun _ ->
    let changelog = getChangelog()
    Directory.ensure "./.nupkg"

    [| sprintf "%O" changelog |] |> File.append "./.nupkg/changelog.md")

Target.create "SetVersionForCI" (fun _ ->
    let changelog = getChangelog()
    printfn "::set-env name=package_version::%s" changelog.NuGetVersion)

Target.create "All" ignore
Target.create "Release" ignore

"Clean" ==> "Restore" ==> "Build" ==> "All"
"Clean" ==> "Restore" ==> "Build" ==> "Publish" ==> "Package" ==> "Changelog" ==> "Release"

Target.runOrDefault "All"
