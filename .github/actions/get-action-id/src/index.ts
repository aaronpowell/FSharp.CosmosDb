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

  const comments = await octokit.issues.listComments({
    ...context.repo,
    issue_number: context.payload.issue.number
  });

  const actionComment = comments.data.find(
    comment => comment.body.indexOf("Action: ") >= 0
  );

  if (!actionComment) {
    throw new Error("No comment found that has the right pattern");
  }

  core.setOutput("id", actionComment.body.replace("Action: ", "").trim());
}

run();
