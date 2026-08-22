import type { HandleServerError } from '@sveltejs/kit';

export const handleError: HandleServerError = async ({ error, event, status, message }) => {
	const errorId = crypto.randomUUID();

    console.error(`Error ID: ${errorId}`, error, { event, status, message });

	return {
		message,
		errorId
	};
};