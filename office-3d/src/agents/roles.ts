export interface RoleConfig {
  id: string;
  name: string;
  shortName: string;
  badge: string;
  color: string;
  colorHex: number;
  deskPos: [number, number, number]; // [x, y, z]
  deskRot: number; // yaw rotation in radians
  outfit: {
    hatColor: number;
    hatType: 'helmet' | 'beret' | 'straw' | 'miner' | 'none';
    vestColor: number;
    bodyColor: number;
    hasGlasses?: boolean;
    hasBackpack?: boolean;
    prop: string;
  };
  idleQuotes: string[];
}

export const ROLES: Record<string, RoleConfig> = {
  orchestrator: {
    id: 'orchestrator',
    name: 'Trưởng nhóm Điều phối',
    shortName: 'Điều phối',
    badge: '🧭',
    color: '#FF8A1F',
    colorHex: 0xFF8A1F,
    deskPos: [0, 0, -9.5],
    deskRot: 0,
    outfit: {
      hatColor: 0xFFFFFF,
      hatType: 'helmet',
      vestColor: 0xFF8A1F,
      bodyColor: 0x334155,
      prop: 'tablet'
    },
    idleQuotes: [
      'Tiến độ đang rất chuẩn chỉ!',
      'Kiểm cổng cẩn thận từng giai đoạn nhé anh em.',
      'Báo cáo Sếp Hồ ngay khi xong tool.'
    ]
  },
  requirements: {
    id: 'requirements',
    name: 'Chuyên viên Yêu cầu',
    shortName: 'Yêu cầu',
    badge: '📝',
    color: '#2DBE70',
    colorHex: 0x2DBE70,
    deskPos: [-13, 0, -3],
    deskRot: Math.PI / 4,
    outfit: {
      hatColor: 0x86EFAC,
      hatType: 'helmet',
      vestColor: 0x22C55E,
      bodyColor: 0xF1F5F9,
      hasGlasses: true,
      prop: 'notes'
    },
    idleQuotes: [
      'Ghi chú nghiệp vụ đã bám sát TCVN.',
      'Sếp yêu cầu tính m² chuẩn từng mét ống gió.',
      'Đặc tả càng rõ thì code càng mượt.'
    ]
  },
  architect: {
    id: 'architect',
    name: 'Kiến trúc sư Solution',
    shortName: 'Kiến trúc',
    badge: '📐',
    color: '#1D5FD1',
    colorHex: 0x1D5FD1,
    deskPos: [-7, 0, -6],
    deskRot: Math.PI / 6,
    outfit: {
      hatColor: 0x1D4ED8,
      hatType: 'helmet',
      vestColor: 0x3B82F6,
      bodyColor: 0x1E293B,
      hasBackpack: true,
      prop: 't_ruler'
    },
    idleQuotes: [
      'Phải tương thích Revit 2024 đến 2027.',
      'Tách riêng Core và Revit API Service.',
      'Mô hình dữ liệu phải nhẹ để cuộn 60 FPS.'
    ]
  },
  researcher: {
    id: 'researcher',
    name: 'Chuyên viên Tra cứu',
    shortName: 'Tra cứu',
    badge: '🔎',
    color: '#F5B700',
    colorHex: 0xF5B700,
    deskPos: [-16, 0, -9],
    deskRot: Math.PI / 2,
    outfit: {
      hatColor: 0x78350F,
      hatType: 'none',
      vestColor: 0xD97706,
      bodyColor: 0x92400E,
      hasGlasses: true,
      prop: 'magnifier'
    },
    idleQuotes: [
      'API này có từ Revit 2019, an toàn 100%.',
      'Tra kỹ tài liệu Autodesk Developer Network.',
      'Phương thức này không lo bị deprecated.'
    ]
  },
  'api-developer': {
    id: 'api-developer',
    name: 'Kỹ sư Lập trình C#',
    shortName: 'Lập trình',
    badge: '⌨️',
    color: '#60A5FA',
    colorHex: 0x60A5FA,
    deskPos: [7, 0, -6],
    deskRot: -Math.PI / 6,
    outfit: {
      hatColor: 0x64748B,
      hatType: 'helmet',
      vestColor: 0x2563EB,
      bodyColor: 0x0F172A,
      prop: 'laptop'
    },
    idleQuotes: [
      'Build thử lần nữa xem 0 lỗi chưa…',
      'Đã đổi sang Selection.SetElementIds siêu nhẹ!',
      'Dùng HashSet O(1) lướt êm ru.'
    ]
  },
  ui: {
    id: 'ui',
    name: 'Chuyên viên Giao diện',
    shortName: 'Giao diện',
    badge: '🎨',
    color: '#A855F7',
    colorHex: 0xA855F7,
    deskPos: [13, 0, -3],
    deskRot: -Math.PI / 4,
    outfit: {
      hatColor: 0x9333EA,
      hatType: 'beret',
      vestColor: 0xC084FC,
      bodyColor: 0x3B0764,
      prop: 'stylus'
    },
    idleQuotes: [
      'Tone xanh navy HadoMEE nhìn rất uy tín.',
      'Bật DoubleBuffered không lo giật nháy hình.',
      'Preview Studio giúp Sếp duyệt trước 100%!'
    ]
  },
  reviewer: {
    id: 'reviewer',
    name: 'Chuyên viên Review',
    shortName: 'Review',
    badge: '🧐',
    color: '#E5484D',
    colorHex: 0xE5484D,
    deskPos: [11, 0, 4],
    deskRot: -Math.PI * 0.75,
    outfit: {
      hatColor: 0xDC2626,
      hatType: 'helmet',
      vestColor: 0xEF4444,
      bodyColor: 0x450A0A,
      hasGlasses: true,
      prop: 'stamp'
    },
    idleQuotes: [
      'Dòng này thiếu Transaction nhé!',
      'Check kỹ xung đột namespace Form và Revit DB.',
      'Code sạch sẽ không còn cảnh báo.'
    ]
  },
  tester: {
    id: 'tester',
    name: 'Kỹ sư Kiểm thử',
    shortName: 'Kiểm thử',
    badge: '🧪',
    color: '#10B981',
    colorHex: 0x10B981,
    deskPos: [7, 0, 8],
    deskRot: Math.PI,
    outfit: {
      hatColor: 0xFACC15,
      hatType: 'helmet',
      vestColor: 0x10B981,
      bodyColor: 0xF8FAFC,
      prop: 'test_tube'
    },
    idleQuotes: [
      'Ca số 7 trên View Combine đã pass!',
      'Test 12/12 ca biên không crash Revit.',
      'Tốc độ phản hồi dưới 0.05 giây.'
    ]
  },
  debugger: {
    id: 'debugger',
    name: 'Bác sĩ Gỡ lỗi',
    shortName: 'Gỡ lỗi',
    badge: '🐞',
    color: '#EAB308',
    colorHex: 0xEAB308,
    deskPos: [1.5, 0, 8.5],
    deskRot: Math.PI,
    outfit: {
      hatColor: 0xFDE047,
      hatType: 'miner',
      vestColor: 0xCA8A04,
      bodyColor: 0x422006,
      prop: 'bug_jar'
    },
    idleQuotes: [
      'Bắt được con bug khóa luồng UI rồi!',
      'Triệt tiêu vòng lặp SelectionChanged thành công.',
      'Hệ thống chạy mát rượi 60 FPS.'
    ]
  },
  security: {
    id: 'security',
    name: 'Chuyên viên An ninh',
    shortName: 'Bảo mật',
    badge: '🛡️',
    color: '#3B82F6',
    colorHex: 0x3B82F6,
    deskPos: [-4.5, 0, 8.5],
    deskRot: Math.PI,
    outfit: {
      hatColor: 0x1E293B,
      hatType: 'helmet',
      vestColor: 0xFACC15,
      bodyColor: 0x0F172A,
      prop: 'shield'
    },
    idleQuotes: [
      'Giữ sạch màn hình Desktop của Sếp 100%.',
      'Chỉ đẩy Git khi Sếp có lệnh rõ ràng.',
      'Quét an toàn dữ liệu không rò rỉ mã nguồn.'
    ]
  },
  packaging: {
    id: 'packaging',
    name: 'Kỹ sư Đóng gói',
    shortName: 'Đóng gói',
    badge: '📦',
    color: '#D97706',
    colorHex: 0xD97706,
    deskPos: [-10, 0, 5],
    deskRot: Math.PI * 0.75,
    outfit: {
      hatColor: 0xB45309,
      hatType: 'helmet',
      vestColor: 0xF59E0B,
      bodyColor: 0x78350F,
      prop: 'tape_gun'
    },
    idleQuotes: [
      'Hàng này đóng gói Version 40 cho Revit 2024!',
      'Setup 1-file .exe cài đặt chỉ 1 click.',
      'Tự động dọn sạch bản cũ, chỉ lưu 2 bản mới.'
    ]
  },
  'docs-writer': {
    id: 'docs-writer',
    name: 'Chuyên viên Tài liệu',
    shortName: 'Tài liệu',
    badge: '📚',
    color: '#8B5CF6',
    colorHex: 0x8B5CF6,
    deskPos: [-15, 0, 1],
    deskRot: Math.PI / 2,
    outfit: {
      hatColor: 0xE2E8F0,
      hatType: 'straw',
      vestColor: 0xA78BFA,
      bodyColor: 0x581C87,
      prop: 'book'
    },
    idleQuotes: [
      'Hướng dẫn sử dụng đã bổ sung hình ảnh chi tiết.',
      'Báo cáo Sếp Hồ: Em làm xong rùi nèee!',
      'Ghi chép đầy đủ tài liệu kỹ thuật.'
    ]
  },
  owner: {
    id: 'owner',
    name: 'Chủ dự án (Sếp Hồ)',
    shortName: 'Sếp Hồ',
    badge: '👑',
    color: '#FF8A1F',
    colorHex: 0xFF8A1F,
    deskPos: [0, 0, 10.5],
    deskRot: Math.PI,
    outfit: {
      hatColor: 0xFF8A1F,
      hatType: 'none',
      vestColor: 0x1D5FD1,
      bodyColor: 0x0F2236,
      prop: 'cup'
    },
    idleQuotes: [
      '★ TẤT CẢ VÌ HÀ ĐÔ THÂN YÊU ★',
      'Làm tool phải mượt mà, không giật lag!',
      'Tập trung vào chất lượng và tốc độ.'
    ]
  }
};

