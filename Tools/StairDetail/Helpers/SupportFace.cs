
using System;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.StairDetail;

public class SupportFace
{
	private ElementId hostId;

	private PlanarFace face;

	private Reference reference;

	private double supportHand;

	private Guid id;

	private bool haveBeamSupport;

	public ElementId HostId
	{
		get
		{
			return hostId;
		}
		set
		{
			hostId = value;
		}
	}

	public PlanarFace Face
	{
		get
		{
			return face;
		}
		set
		{
			face = value;
		}
	}

	public double SupportHand
	{
		get
		{
			return supportHand;
		}
		set
		{
			supportHand = value;
		}
	}

	public Reference Reference
	{
		get
		{
			return reference;
		}
		set
		{
			reference = value;
		}
	}

	public Guid Id
	{
		get
		{
			return id;
		}
		set
		{
			id = value;
		}
	}

	public bool HaveBeamSupport
	{
		get
		{
			return haveBeamSupport;
		}
		set
		{
			haveBeamSupport = value;
		}
	}

	public SupportFace()
	{
		reference = null;
		id = Guid.NewGuid();
		haveBeamSupport = true;
	}

	public SupportFace(ElementId m_hostId, Reference m_reference, PlanarFace m_face, double m_hand)
	{
		hostId = m_hostId;
		face = m_face;
		supportHand = m_hand;
		reference = m_reference;
		id = Guid.NewGuid();
		haveBeamSupport = true;
	}
}



