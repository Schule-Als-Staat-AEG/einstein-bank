import { getAuthStore } from './auth';
import { server } from './connections';

let obfuscationMap: Record<string, string> = {
	'4': '0',
	'3': '1',
	c: '2',
	t: '3',
	'7': '4',
	'0': '5',
	'1': '6',
	'9': '7',
	'8': '8',
	'2': '9',
	b: '+',
	'5': '-',
	'6': '*'
};

export function solveCaptcha(question: string): string {
	let deobfuscated = '';
	let questionChars = question.split('');
	questionChars.forEach((char) => {
		deobfuscated += obfuscationMap[char] || char;
	});

	// Solve the equation
	// Split the 3 numbers and 2 operators
	const match = deobfuscated.match(/(\d+)([+\-*])(\d+)([+\-*])(\d+)/);
	if (!match) {
		throw new Error('Invalid captcha format');
	}
	const num1 = parseInt(match[1]);
	const op1 = match[2];
	const num2 = parseInt(match[3]);
	const op2 = match[4];
	const num3 = parseInt(match[5]);

	let result = 0;
	switch (op1) {
		case '+':
			result = num1 + num2;
			break;
		case '-':
			result = num1 - num2;
			break;
		case '*':
			result = num1 * num2;
			break;
	}
	switch (op2) {
		case '+':
			result += num3;
			break;
		case '-':
			result -= num3;
			break;
		case '*':
			result *= num3;
			break;
	}
	if (op2 === '*' && op1 !== '*') {
		switch (op1) {
			case '+':
				result = num1 + num2 * num3;
				break;
			case '-':
				result = num1 - num2 * num3;
				break;
		}
	}
	result -= 17;
	return result.toString();
}

export async function fetchCaptcha(): Promise<{ captchaID: string; question: string }> {
	const response = await fetch(server + '/api/generate-captcha', {
		method: 'GET',
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${getAuthStore().token}`
		}
	});
	if (!response.ok) {
		throw new Error('Failed to fetch captcha');
	}
	const data = await response.json();
	return { captchaID: data.captchaID, question: data.captchaQuestion };
}
