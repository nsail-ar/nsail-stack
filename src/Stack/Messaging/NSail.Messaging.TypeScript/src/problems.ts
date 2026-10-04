// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

export interface Issue {
  code: string;
  message: string;
  source?: string | null;
  arguments?: Record<string, string> | null;
}

export interface Problem {
  code: string;
  title: string;
  issues: Issue[];
  status?: number | null;
}

/** The TypeScript face of BusinessException: an expected failure carrying the Problem the
 * server (or the client's own validation) answered. */
export class BusinessError extends Error {
  readonly problem: Problem;

  constructor(problem: Problem) {
    super(problem.title);
    this.name = "BusinessError";
    this.problem = problem;
  }
}

export function isProblem(value: unknown): value is Problem {
  return (
    typeof value === "object" &&
    value !== null &&
    typeof (value as Problem).code === "string" &&
    typeof (value as Problem).title === "string"
  );
}

/** The same codes NetworkProblem.RequestFailed carries, so one catalog translates both ends. */
export function requestFailed(status: number | null, reason: "EmptyResponse" | "DeserializationFailed" | "StatusFallback" | null): Problem {
  const issues: Issue[] = [
    status
      ? { code: "HttpStatus", message: `The server responded with HTTP status code ${status}.` }
      : { code: "NoResponse", message: "No response was received from the server." },
  ];

  if (reason) {
    issues.push({ code: reason, message: reason });
  }

  return { code: "RequestFailed", title: "Unable to contact the server", issues, status };
}

/** Issues about one field, matched by the C# member name a server Issue names in its source. */
export function issuesFor(problem: Problem | null | undefined, member: string): Issue[] {
  if (!problem) {
    return [];
  }

  return problem.issues.filter((issue) => issue.source === member);
}
