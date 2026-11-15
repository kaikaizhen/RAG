/** 瀏覽器端元件也會用到的型別，lib/api 是 server-only 所以放這裡。 */
export interface McpToken {
  id: number;
  name: string;
  created_at: string;
  last_used_at: string | null;
}
