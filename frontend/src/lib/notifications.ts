import { browser } from '$app/environment';
import { goto } from '$app/navigation';
import db from './connections';

function navigateToNotifications(link: string) {
	if (!browser) return;
	if (!link) {
		return;
	}
	let target = link.split('#')[0];
	if (link.split('#').length > 1) {
		let data = link.split('#')[1] || null;
		let exact = '';
		if (link.split('#').length > 2) {
			exact = link.split('#')[2];
		}
		if (target == 'transactions') {
			goto('/transactions?id=' + data);
		}
		if (target == 'orders')
		{
			goto('/warehouse?id=' + data);
		}
		if (target == 'ticket')
		{
			goto('/support/' + data);
		}
	}
}
function markAsRead(notificationId: string) {
	db.collection('notifications').update(notificationId, { isRead: true }, { requestKey: null });
}
export { navigateToNotifications, markAsRead };
