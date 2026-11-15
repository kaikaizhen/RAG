/** 呼叫端可以修正的錯誤，訊息會回傳給 Claude。其他錯誤只記 log，不外洩內容。 */
export class NotesError extends Error {}
