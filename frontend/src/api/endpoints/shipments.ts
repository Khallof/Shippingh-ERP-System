import { api } from '../client';
import type {
  AssignDriverRequest, CancelShipmentRequest, ChangeShipmentStatusRequest, CorrectShipmentStatusRequest, DeliverShipmentRequest,
  LabelFormat, ProofOfDeliveryDto, RecordDeliveryFailureRequest, SaveShipmentRequest, ShipmentAttachmentDto, ShipmentDto,
  ShipmentListItemDto, ShipmentListQuery, ShipmentPricingDto, ShipmentStatusHistoryDto, ShipmentTrackingDto, ShipmentAttachmentType
} from '../generated';
import type { ListParams } from '../types';

/**
 * Typed endpoints of the shipments module (one file per module keeps URLs in one place).
 * Other modules use the generic api.get / api.list / api.post with their DTO types.
 */
export const shipmentsApi = {
  list: (query: ShipmentListQuery | ListParams) => api.list<ShipmentListItemDto>('/shipments', query as ListParams),
  get: (id: number) => api.get<ShipmentDto>(`/shipments/${id}`),
  track: (code: string) => api.get<ShipmentTrackingDto>(`/shipments/track/${encodeURIComponent(code)}`),
  history: (id: number) => api.get<ShipmentStatusHistoryDto[]>(`/shipments/${id}/history`),
  pricing: (id: number) => api.get<ShipmentPricingDto[]>(`/shipments/${id}/pricing`),
  quote: (request: SaveShipmentRequest) => api.post<ShipmentPricingDto>('/shipments/quote', request),
  create: (request: SaveShipmentRequest) => api.post<ShipmentDto>('/shipments', request),
  update: (id: number, request: SaveShipmentRequest) => api.put<ShipmentDto>(`/shipments/${id}`, request),
  changeStatus: (id: number, request: ChangeShipmentStatusRequest) => api.patch<ShipmentDto>(`/shipments/${id}/status`, request),
  correctStatus: (id: number, request: CorrectShipmentStatusRequest) => api.post<ShipmentDto>(`/shipments/${id}/status-corrections`, request),
  cancel: (id: number, request: CancelShipmentRequest) => api.post<ShipmentDto>(`/shipments/${id}/cancel`, request),
  assignDriver: (id: number, request: AssignDriverRequest) => api.patch<ShipmentDto>(`/shipments/${id}/driver`, request),

  /** Proof of delivery: multipart (recipientName, recipientRelation, notes, latitude, longitude, signature, photo). */
  deliver: (id: number, request: DeliverShipmentRequest, signature?: Blob | null, photo?: File | null) => {
    const form = new FormData();
    Object.entries(request).forEach(([key, value]) => {
      if (value !== null && value !== undefined && value !== '') {
        form.append(key, String(value));
      }
    });
    if (signature) form.append('signature', signature, 'signature.png');
    if (photo) form.append('photo', photo, photo.name);
    return api.postForm<ProofOfDeliveryDto>(`/shipments/${id}/deliver`, form);
  },
  deliveryFailed: (id: number, request: RecordDeliveryFailureRequest) => api.post<ShipmentDto>(`/shipments/${id}/delivery-failed`, request),
  proofOfDelivery: (id: number) => api.get<ProofOfDeliveryDto>(`/shipments/${id}/proof-of-delivery`),

  attachments: (id: number) => api.get<ShipmentAttachmentDto[]>(`/shipments/${id}/attachments`),
  uploadAttachment: (id: number, file: File, attachmentType: ShipmentAttachmentType, description?: string) => {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('attachmentType', attachmentType);
    if (description) form.append('description', description);
    return api.postForm<ShipmentAttachmentDto>(`/shipments/${id}/attachments`, form);
  },
  deleteAttachment: (id: number, attachmentId: number) => api.delete(`/shipments/${id}/attachments/${attachmentId}`),

  /** Thermal label (one page per package). */
  label: (id: number, format: LabelFormat = 'Pdf') => api.download(`/shipments/${id}/label`, { params: { format }, fallbackName: 'label.pdf' }),
  labels: (shipmentIds: number[], format: LabelFormat = 'Pdf') =>
    api.download('/shipments/labels', { method: 'post', body: { shipmentIds, format }, fallbackName: 'labels.pdf' })
};
