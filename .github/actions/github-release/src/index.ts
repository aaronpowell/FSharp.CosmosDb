import * as core from "@actions/core";
import * as github from "@actions/github";
import { lstatSync, readFileSync } from "fs";
import { getType } from "mime";
import { basename, join } from "path";
import { Context } from "@actions/github/lib/context";

function mimeOrDefault(path: string) {
  return getType(path) || "application/octet-stream";
}

function fileInfo(path: string) {
  return {
    name: basename(path),
    mime: mimeOrDefault(path),
    size: lstatSync(path).size,
    file: readFileSync(path)
  };
}

async function upload(
  octokit: github.GitHub,
  context: Context,
  url: string,
  path: string
) {
  let { name, mime, size, file } = fileInfo(path);
  console.log(`Uploading ${name}...`);
  await octokit.repos.uploadReleaseAsset({
    ...context.repo,
    name,
    file,
    url,
    headers: {
      "content-length": size,
      "content-type": mime
    }
  });
}

async function run() {
  const token = core.getInput("token");
  const sha = core.getInput("sha");
  const version = core.getInput("version");
  const artifactPath = core.getInput("path");

  const releaseNotes = readFileSync(join(artifactPath, "changelog.md"), {
    encoding: "UTF8"
  });

  const octokit = new github.GitHub(token);
  const context = github.context;

  const release = await octokit.repos.createRelease({
    ...context.repo,
    tag_name: version,
    target_commitish: sha,
    name: `Release ${version}`,
    body: releaseNotes
  });

  await upload(
    octokit,
    context,
    release.data.upload_url,
    join(artifactPath, `FSharp.CosmosDb.${version}.nupkg`)
  );
  await upload(
    octokit,
    context,
    release.data.upload_url,
    join(artifactPath, `FSharp.CosmosDb.Analyzer.${version}.nupkg`)
  );
}

run();
