/**
 * CRUD 端點的回應型別。
 *
 * 這些 Controller 沒有宣告 ProducesResponseType，OpenAPI snapshot 不含回應 schema，
 * 因此這裡手寫對應後端 ViewModel 的型別（RAG/Models/ViewModels）。
 * 日後在 Controller 補上 ProducesResponseType 並更新 snapshot 後，應改為由 generated/schema.ts 衍生。
 */

/** 知識狀態；Superseded 的項目不參與檢索 */
export type KnowledgeStatus = 'Verified' | 'Stale' | 'Inferred' | 'Superseded';

/** 使用者管理層級（LoginVM.manage / GetUserVM.manage） */
export type ManageLevel = 'free' | 'pro' | 'max' | (string & {});

export interface QaType {
  id: number;
  nameZh: string;
  nameEn: string;
  nameOther: string;
}

export interface QaTypeItem {
  id: number;
  qaTypeID: number;
  nameZh: string;
  nameEn: string;
  nameOther: string;
  isActive: boolean;
}

export interface Question {
  id: number;
  qaItemTypeID: number;
  userID: number;
  question: string;
  answer: string;
  createAt: string;
  updateAt: string | null;
  status: KnowledgeStatus | (string & {});
}

export interface User {
  id: number;
  account: string;
  nameZh: string;
  nameEn: string;
  nameOther: string;
  manage: ManageLevel;
  isActive: boolean;
}

/** 寫入類端點的共同回應 */
export interface MutationResult {
  state: string;
  message: string;
}

/** GET /Question 的查詢方式（對應後端 QuestionEnum） */
export const QuestionQueryType = { All: 0, Type: 1 } as const;
