import cronstrue from 'cronstrue';
import 'cronstrue/locales/de';

export default function cronToGerman(expression: string, adjust: boolean = true): string {
	try {
		const offset = -new Date().getTimezoneOffset() / 60;

		const adjusted = adjust ? adjustCronTimezone(expression, offset) : expression;

		return cronstrue.toString(adjusted, {
			locale: 'de',
			use24HourTimeFormat: true
		});
	} catch {
		return 'Ungültiger Cron-Ausdruck';
	}
}
function adjustCronTimezone(expression: string, offsetHours: number): string {
	const parts = expression.split(' ');

	if (parts.length !== 5) {
		return expression;
	}

	const minute = parts[0];
	const hour = parts[1];

	if (/^\d+$/.test(hour)) {
		let newHour = (parseInt(hour, 10) + offsetHours) % 24;
		if (newHour < 0) newHour += 24;

		parts[1] = newHour.toString();
	}

	return parts.join(' ');
}
