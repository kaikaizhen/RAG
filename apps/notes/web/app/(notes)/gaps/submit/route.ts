import { addGap, resolveGap } from '@/lib/api';

function redirect(location: string): Response {
  return new Response(null, { status: 303, headers: { Location: location } });
}

/** 登入 cookie 是 SameSite=Lax，其他網站的表單送不到這裡。 */
export async function POST(request: Request) {
  const form = await request.formData();
  const resolve = form.get('resolve');
  if (typeof resolve === 'string' && /^\d+$/.test(resolve)) {
    await resolveGap(resolve);
    return redirect('/questions');
  }
  const question = form.get('question');
  if (typeof question !== 'string' || !question.trim()) return redirect('/gaps/new');
  const result = await addGap(question);
  return redirect(result.status === 201 ? '/questions?gap=added' : '/questions?gap=rejected');
}
