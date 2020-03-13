import * as core from "@actions/core";
import * as github from "@actions/github";

async function run() {
  const token = core.getInput("token");

  const octokit = new github.GitHub(token);
  const context = github.context;

  if (!context.payload.issue) {
    throw new Error("This should not happen");
  }

  await octokit.issues.createComment({
    ...context.repo,
    issue_number: context.payload.issue.number,
    body: core.getInput("message")
  });

  await octokit.issues.update({
    ...context.repo,
    issue_number: context.payload.issue.number,
    state: "closed"
  });
}

run();
