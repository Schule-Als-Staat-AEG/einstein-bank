import PocketBase from 'pocketbase';
import { env } from '$env/dynamic/public';

const DB_URL = env.PUBLIC_DB_URL || 'https://yourdomain.com';

const SV_URL = env.PUBLIC_SV_URL || 'https://sv.yourdomain.com';

const db = new PocketBase(DB_URL);
db.autoCancellation(false);
const server = SV_URL;

export default db;
export { server };
