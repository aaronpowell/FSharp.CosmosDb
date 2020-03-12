import * as core from "@actions/core";
import * as github from "@actions/github";

async function run() {
  const token = core.getInput("token");

  const octokit = new github.GitHub(token);
  const context = github.context;

  if (!context.payload.issue) {
    throw new Error("This should not happen");
  }

  const issue = await octokit.issues.get({
    ...context.repo,
    issue_number: context.payload.issue.number
  });

  core.setOutput(
    "exists",
    issue.data.labels
      .some(label => label.name === core.getInput("label"))
      .toString()
  );
}

run();
