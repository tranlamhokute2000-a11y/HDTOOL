export type ZoneId =
  | 'all'
  | 'executive'
  | 'architecture'
  | 'engineering'
  | 'council'
  | 'verification'
  | 'auditorium'
  | 'reception';

export interface ZoneConfig {
  id: ZoneId;
  label: string;
  tagText: string;
  cameraPos: [number, number, number];
  lookAt: [number, number, number];
}

export const ZONES: ZoneConfig[] = [
  {
    id: 'all',
    label: 'Toàn cảnh',
    tagText: 'TOÀN CẢNH CAMPUS',
    cameraPos: [0, 26, 30],
    lookAt: [0, 0, 1]
  },
  {
    id: 'executive',
    label: 'Điều phối',
    tagText: '# ĐIỀU PHỐI',
    cameraPos: [-15, 11, 4],
    lookAt: [-15, 0.8, -8.5]
  },
  {
    id: 'architecture',
    label: 'Hình học & Kiến trúc',
    tagText: '# HÌNH HỌC & API',
    cameraPos: [-3, 12, 5],
    lookAt: [-3, 0.8, -7.5]
  },
  {
    id: 'engineering',
    label: 'Lập trình & Giao diện',
    tagText: '# LẬP TRÌNH & UI',
    cameraPos: [12, 12, 5],
    lookAt: [12, 0.8, -7.5]
  },
  {
    id: 'council',
    label: 'Hội đồng Swarm',
    tagText: '# HỘI ĐỒNG SWARM',
    cameraPos: [-1, 10, 12],
    lookAt: [-1, 0.8, 1.5]
  },
  {
    id: 'verification',
    label: 'Kiểm thử & Build',
    tagText: '# KIỂM THỬ & BUILD',
    cameraPos: [14, 10, 12],
    lookAt: [14, 0.8, 2]
  },
  {
    id: 'auditorium',
    label: 'Hội trường',
    tagText: '# HỘI TRƯỜNG',
    cameraPos: [-8, 11, 20],
    lookAt: [-9, 1, 9.5]
  },
  {
    id: 'reception',
    label: 'Lễ tân',
    tagText: '# LỄ TÂN',
    cameraPos: [12, 10, 20],
    lookAt: [12, 0.8, 9.5]
  }
];

export interface RoleConfig {
  id: string;
  name: string;
  shortName: string;
  badge: string;
  zone: ZoneId;
  zoneLabel: string;
  color: string;
  colorHex: number;
  deskPos: [number, number, number];
  deskRot: number;
  isSwarmDefault?: boolean;
  outfit: {
    hatColor: number;
    hatType: 'helmet' | 'beret' | 'headset' | 'cap' | 'none';
    vestColor: number;
    bodyColor: number;
    hasGlasses?: boolean;
    hasBackpack?: boolean;
    prop: string;
  };
  idleQuotes: string[];
}

export const ROLES: Record<string, RoleConfig> = {
  // === ĐỘI SWARM TRỰC TIẾP (CHÍNH) ===
  coordinator: {
    id: 'coordinator',
    name: 'Điều phối Swarm (Coordinator)',
    shortName: 'coordinator',
    badge: '🧭',
    zone: 'executive',
    zoneLabel: 'Điều phối',
    color: '#EA580C',
    colorHex: 0xEA580C,
    deskPos: [-15.2, 0, -9.2],
    deskRot: 0,
    isSwarmDefault: true,
    outfit: {
      hatColor: 0xFFFFFF,
      hatType: 'headset',
      vestColor: 0xF97316,
      bodyColor: 0x1E293B,
      hasGlasses: true,
      prop: 'tablet'
    },
    idleQuotes: [
      'Đang chia việc song song cho đội Swarm.',
      'Chờ báo cáo kiểm thử từ @verifier.',
      'Đảm bảo sạch lỗi C# 5 trước khi đóng gói!'
    ]
  },
  geometry: {
    id: 'geometry',
    name: 'Kỹ sư Hình học CAD-Duct',
    shortName: 'geometry',
    badge: '📐',
    zone: 'architecture',
    zoneLabel: 'Hình học & Kiến trúc',
    color: '#2563EB',
    colorHex: 0x2563EB,
    deskPos: [-6.5, 0, -9.2],
    deskRot: 0,
    isSwarmDefault: true,
    outfit: {
      hatColor: 0x2563EB,
      hatType: 'helmet',
      vestColor: 0x3B82F6,
      bodyColor: 0x1E293B,
      hasGlasses: true,
      prop: 't_ruler'
    },
    idleQuotes: [
      'Đang phân tích hình học Arc & PolyLine.',
      'Bắt cặp 2 cung cút cong 1900x400.',
      'Lan truyền kích thước BFS tuyến gần nhất.'
    ]
  },
  ui: {
    id: 'ui',
    name: 'Kỹ sư Giao diện & Tài liệu',
    shortName: 'ui',
    badge: '🎨',
    zone: 'engineering',
    zoneLabel: 'Lập trình & Giao diện',
    color: '#9333EA',
    colorHex: 0x9333EA,
    deskPos: [9.2, 0, -9.2],
    deskRot: 0,
    isSwarmDefault: true,
    outfit: {
      hatColor: 0x9333EA,
      hatType: 'beret',
      vestColor: 0xA855F7,
      bodyColor: 0x312E81,
      prop: 'stylus'
    },
    idleQuotes: [
      'Form Modeless 100%, không bị cắt chữ!',
      'Đã bỏ cao độ mặc định 3200 theo lệnh Sếp.',
      'Đồng bộ Skill và Preview Studio.'
    ]
  },
  verifier: {
    id: 'verifier',
    name: 'Chuyên viên Kiểm định & Build',
    shortName: 'verifier',
    badge: '🛡️',
    zone: 'verification',
    zoneLabel: 'Kiểm thử & Build',
    color: '#059669',
    colorHex: 0x059669,
    deskPos: [12.0, 0, 0.8],
    deskRot: Math.PI,
    isSwarmDefault: true,
    outfit: {
      hatColor: 0xFACC15,
      hatType: 'helmet',
      vestColor: 0x10B981,
      bodyColor: 0x0F172A,
      hasGlasses: true,
      prop: 'stamp'
    },
    idleQuotes: [
      'Đang rà soát diff C# 5 (.NET 4.8).',
      'Chạy Build.ps1 xuất Installer 0 lỗi!',
      'Kiểm tra giữ 2 Version và Desktop sạch.'
    ]
  },
  monitor: {
    id: 'monitor',
    name: 'Giám sát Tiến độ Swarm',
    shortName: 'monitor',
    badge: '📡',
    zone: 'council',
    zoneLabel: 'Hội đồng Swarm',
    color: '#D97706',
    colorHex: 0xD97706,
    deskPos: [-4.2, 0, 1.4],
    deskRot: Math.PI / 2,
    isSwarmDefault: true,
    outfit: {
      hatColor: 0xF59E0B,
      hatType: 'headset',
      vestColor: 0xF59E0B,
      bodyColor: 0x334155,
      prop: 'tablet'
    },
    idleQuotes: [
      'Theo dõi nhịp board, phát hiện tắc nghẽn.',
      'Nhắc nhẹ khi thành viên đứng chờ lâu.',
      'Tự động nghỉ khi thấy tín hiệu [done].'
    ]
  },

  // === ĐỘI NGŨ CHUYÊN TRÁCH REVIT 12 AGENT ===
  orchestrator: {
    id: 'orchestrator',
    name: 'Điều phối Hệ thống Revit',
    shortName: 'Điều phối',
    badge: '👔',
    zone: 'executive',
    zoneLabel: 'Điều phối',
    color: '#EA580C',
    colorHex: 0xEA580C,
    deskPos: [-12.0, 0, -6.5],
    deskRot: -Math.PI / 4,
    outfit: {
      hatColor: 0x1E293B,
      hatType: 'none',
      vestColor: 0xEA580C,
      bodyColor: 0x0F172A,
      prop: 'tablet'
    },
    idleQuotes: ['Kiểm cổng giai đoạn chuẩn xác.']
  },
  requirements: {
    id: 'requirements',
    name: 'Chuyên viên Đặc tả (Spec)',
    shortName: 'Đặc tả',
    badge: '📝',
    zone: 'architecture',
    zoneLabel: 'Hình học & Kiến trúc',
    color: '#16A34A',
    colorHex: 0x16A34A,
    deskPos: [-6.5, 0, -5.2],
    deskRot: Math.PI,
    outfit: {
      hatColor: 0x4ADE80,
      hatType: 'cap',
      vestColor: 0x22C55E,
      bodyColor: 0x334155,
      hasGlasses: true,
      prop: 'notes'
    },
    idleQuotes: ['Chuẩn hóa quy chuẩn kỹ thuật HVAC.']
  },
  architect: {
    id: 'architect',
    name: 'Kiến trúc sư Giải pháp',
    shortName: 'Kiến trúc',
    badge: '🏛️',
    zone: 'architecture',
    zoneLabel: 'Hình học & Kiến trúc',
    color: '#1D4ED8',
    colorHex: 0x1D4ED8,
    deskPos: [-1.5, 0, -9.2],
    deskRot: 0,
    outfit: {
      hatColor: 0x1D4ED8,
      hatType: 'helmet',
      vestColor: 0x2563EB,
      bodyColor: 0x1E293B,
      hasBackpack: true,
      prop: 't_ruler'
    },
    idleQuotes: ['Tách Core và Service sạch sẽ.']
  },
  researcher: {
    id: 'researcher',
    name: 'Chuyên viên Tra cứu API',
    shortName: 'Tra cứu',
    badge: '🔎',
    zone: 'architecture',
    zoneLabel: 'Hình học & Kiến trúc',
    color: '#CA8A04',
    colorHex: 0xCA8A04,
    deskPos: [-1.5, 0, -5.2],
    deskRot: Math.PI,
    outfit: {
      hatColor: 0x854D0E,
      hatType: 'cap',
      vestColor: 0xEAB308,
      bodyColor: 0x422006,
      hasGlasses: true,
      prop: 'magnifier'
    },
    idleQuotes: ['Tra cứu Revit API 2024–2027.']
  },
  'api-developer': {
    id: 'api-developer',
    name: 'Lập trình viên Revit C#',
    shortName: 'Lập trình C#',
    badge: '⌨️',
    zone: 'engineering',
    zoneLabel: 'Lập trình & Giao diện',
    color: '#0284C7',
    colorHex: 0x0284C7,
    deskPos: [14.5, 0, -9.2],
    deskRot: 0,
    outfit: {
      hatColor: 0x0284C7,
      hatType: 'headset',
      vestColor: 0x0EA5E9,
      bodyColor: 0x0F172A,
      prop: 'laptop'
    },
    idleQuotes: ['FilteredElementCollector tối ưu tốc độ!']
  },
  reviewer: {
    id: 'reviewer',
    name: 'Thẩm định Mã nguồn',
    shortName: 'Review',
    badge: '🧐',
    zone: 'verification',
    zoneLabel: 'Kiểm thử & Build',
    color: '#DC2626',
    colorHex: 0xDC2626,
    deskPos: [16.5, 0, 0.8],
    deskRot: Math.PI,
    outfit: {
      hatColor: 0xDC2626,
      hatType: 'cap',
      vestColor: 0xEF4444,
      bodyColor: 0x450A0A,
      hasGlasses: true,
      prop: 'stamp'
    },
    idleQuotes: ['Không rò rỉ bộ nhớ, chuẩn C# 5.']
  },
  tester: {
    id: 'tester',
    name: 'Kỹ sư Kiểm thử (QA)',
    shortName: 'Kiểm thử',
    badge: '🧪',
    zone: 'verification',
    zoneLabel: 'Kiểm thử & Build',
    color: '#0D9488',
    colorHex: 0x0D9488,
    deskPos: [12.0, 0, 3.8],
    deskRot: 0,
    outfit: {
      hatColor: 0x14B8A6,
      hatType: 'helmet',
      vestColor: 0x14B8A6,
      bodyColor: 0x134E4A,
      prop: 'test_tube'
    },
    idleQuotes: ['Tất cả kịch bản biên đã pass!']
  },
  debugger: {
    id: 'debugger',
    name: 'Chuyên gia Sửa lỗi',
    shortName: 'Gỡ lỗi',
    badge: '🐞',
    zone: 'verification',
    zoneLabel: 'Kiểm thử & Build',
    color: '#E11D48',
    colorHex: 0xE11D48,
    deskPos: [16.5, 0, 3.8],
    deskRot: 0,
    outfit: {
      hatColor: 0xF43F5E,
      hatType: 'headset',
      vestColor: 0xFB7185,
      bodyColor: 0x4C0519,
      prop: 'magnifier'
    },
    idleQuotes: ['Truy vết tận gốc không để lag!']
  },
  security: {
    id: 'security',
    name: 'Chuyên gia Bảo mật',
    shortName: 'Bảo mật',
    badge: '🔒',
    zone: 'council',
    zoneLabel: 'Hội đồng Swarm',
    color: '#4F46E5',
    colorHex: 0x4F46E5,
    deskPos: [2.2, 0, 1.4],
    deskRot: -Math.PI / 2,
    outfit: {
      hatColor: 0x4F46E5,
      hatType: 'helmet',
      vestColor: 0x6366F1,
      bodyColor: 0x1E1B4B,
      prop: 'tablet'
    },
    idleQuotes: ['Giữ sạch Desktop, bảo vệ dữ liệu Sếp.']
  },
  packaging: {
    id: 'packaging',
    name: 'Kỹ sư Đóng gói Setup',
    shortName: 'Đóng gói',
    badge: '📦',
    zone: 'auditorium',
    zoneLabel: 'Hội trường',
    color: '#B45309',
    colorHex: 0xB45309,
    deskPos: [-13.5, 0, 8.8],
    deskRot: Math.PI / 6,
    outfit: {
      hatColor: 0xD97706,
      hatType: 'cap',
      vestColor: 0xF59E0B,
      bodyColor: 0x451A03,
      prop: 'box'
    },
    idleQuotes: ['Đóng gói Setup 1-file chuẩn xác.']
  },
  'docs-writer': {
    id: 'docs-writer',
    name: 'Chuyên viên Tài liệu & Skill',
    shortName: 'Tài liệu',
    badge: '📚',
    zone: 'auditorium',
    zoneLabel: 'Hội trường',
    color: '#0891B2',
    colorHex: 0x0891B2,
    deskPos: [-5.5, 0, 8.8],
    deskRot: -Math.PI / 6,
    outfit: {
      hatColor: 0x06B6D4,
      hatType: 'beret',
      vestColor: 0x22D3EE,
      bodyColor: 0x164E63,
      prop: 'notes'
    },
    idleQuotes: ['Đúc kết Skill và cập nhật hướng dẫn.']
  },
  owner: {
    id: 'owner',
    name: 'Sếp Hồ (Chỉ huy trưởng)',
    shortName: 'Sếp Hồ',
    badge: '👑',
    zone: 'reception',
    zoneLabel: 'Lễ tân',
    color: '#D97706',
    colorHex: 0xF59E0B,
    deskPos: [12.0, 0, 10.2],
    deskRot: Math.PI,
    outfit: {
      hatColor: 0xFBBF24,
      hatType: 'none',
      vestColor: 0xF59E0B,
      bodyColor: 0x0F172A,
      hasGlasses: true,
      prop: 'tablet'
    },
    idleQuotes: [
      'Đang giám sát toàn bộ đội Worker!',
      'Kiểm tra xem đội có chạy mượt không.',
      'Triển khai nhanh, gọn, đúng trọng tâm!'
    ]
  }
};

// Dynamic desk overflow pool inside Meeting / Engineering pods for ad-hoc workers
const SPARE_DESK_SLOTS: Array<{ zone: ZoneId; zoneLabel: string; pos: [number, number, number]; rot: number }> = [
  { zone: 'engineering', zoneLabel: 'Lập trình & Giao diện', pos: [9.2, 0, -5.2], rot: Math.PI },
  { zone: 'engineering', zoneLabel: 'Lập trình & Giao diện', pos: [14.5, 0, -5.2], rot: Math.PI },
  { zone: 'council', zoneLabel: 'Hội đồng Swarm', pos: [-1.0, 0, -0.3], rot: 0 },
  { zone: 'council', zoneLabel: 'Hội đồng Swarm', pos: [-1.0, 0, 3.1], rot: Math.PI },
  { zone: 'reception', zoneLabel: 'Lễ tân', pos: [8.0, 0, 9.8], rot: -Math.PI / 4 },
  { zone: 'reception', zoneLabel: 'Lễ tân', pos: [16.0, 0, 9.8], rot: Math.PI / 4 }
];

const DYNAMIC_PALETTE = [
  { color: '#0EA5E9', hex: 0x0EA5E9, vest: 0x38BDF8, body: 0x0C4A6E },
  { color: '#EC4899', hex: 0xEC4899, vest: 0xF472B6, body: 0x500724 },
  { color: '#8B5CF6', hex: 0x8B5CF6, vest: 0xA78BFA, body: 0x2E1065 },
  { color: '#14B8A6', hex: 0x14B8A6, vest: 0x2DD4BF, body: 0x134E4A },
  { color: '#F97316', hex: 0xF97316, vest: 0xFB923C, body: 0x431407 }
];

export function resolveDynamicRole(workerId: string, spareIndex: number, displayName?: string): RoleConfig {
  const normalized = workerId.toLowerCase();
  if (ROLES[normalized]) return ROLES[normalized];

  // Match aliases cleanly (e.g., sub-1234 with role 'Research' -> researcher-like)
  const slot = SPARE_DESK_SLOTS[spareIndex % SPARE_DESK_SLOTS.length];
  const pal = DYNAMIC_PALETTE[spareIndex % DYNAMIC_PALETTE.length];

  return {
    id: workerId,
    name: displayName || `Worker ${workerId}`,
    shortName: workerId,
    badge: normalized.startsWith('sub-') ? '🤖' : '⚡',
    zone: slot.zone,
    zoneLabel: slot.zoneLabel,
    color: pal.color,
    colorHex: pal.hex,
    deskPos: slot.pos,
    deskRot: slot.rot,
    outfit: {
      hatColor: pal.hex,
      hatType: 'headset',
      vestColor: pal.vest,
      bodyColor: pal.body,
      prop: 'laptop'
    },
    idleQuotes: ['Đang sẵn sàng hỗ trợ Swarm!']
  };
}
