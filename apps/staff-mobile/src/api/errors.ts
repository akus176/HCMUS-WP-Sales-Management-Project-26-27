export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: {
      type?: string;
      title?: string;
      detail?: string;
      code?: string;
      errors?: Record<string, string[]>;
    } = {},
  ) {
    super(
      status === 401
        ? 'Sign-in failed or your session has expired.'
        : status === 403
          ? 'You do not have permission for this action.'
          : status === 409
            ? 'This record has changed. Refresh before trying again.'
            : status === 400 || status === 422
              ? 'Check the submitted information and try again.'
              : 'The service could not complete the request.',
    );
    this.name = 'ApiError';
  }
}

export class NetworkError extends Error {
  constructor() {
    super('Cannot reach the service. Check your connection and try again.');
    this.name = 'NetworkError';
  }
}

export function cancelled() {
  const error = new Error('Request cancelled.');
  error.name = 'AbortError';
  return error;
}

export async function requireSuccess(response: Response) {
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    const problem = body && typeof body === 'object' ? body : {};
    throw new ApiError(response.status, {
      type: problem.type,
      title: problem.title,
      detail: problem.detail,
      code: problem.code,
      errors: problem.errors,
    });
  }
  return response;
}
