/**
 * Authentication Services
 * Centralized exports for all authentication providers
 */

import { feideAuth as feideAuthInstance } from './feide';
import { bankIDAuth as bankIDAuthInstance } from './bankid';

export const feideAuth = feideAuthInstance;
export const bankIDAuth = bankIDAuthInstance;
