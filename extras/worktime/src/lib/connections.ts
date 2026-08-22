import PocketBase from 'pocketbase';

/** Change these two values for your deployment. */
export const POCKETBASE_URL = 'https://arbeitszeit.yourdomain.com';
export const SERVER_URL = 'https://server.yourdomain.com';

/** Shared PocketBase client used throughout the management interface. */
export const db = new PocketBase(POCKETBASE_URL);

export default db;
