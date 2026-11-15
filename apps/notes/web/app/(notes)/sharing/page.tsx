import { Share2 } from 'lucide-react';
import { SharingManager } from '@/components/SharingManager';
import { PageHeader } from '@/components/ui';
import { getSharing } from '@/lib/api';

export default async function SharingPage() {
  const { projects, unassigned_concepts, status } = await getSharing();

  return (
    <>
      <PageHeader title="分享範圍" icon={<Share2 size={18} strokeWidth={1.8} aria-hidden="true" />}>
        每個專案自己決定誰問得到。新專案一律從「只留在筆記」開始。
      </PageHeader>
      <SharingManager projects={projects} unassignedConcepts={unassigned_concepts} status={status} />
    </>
  );
}
